using System.Data;
using System.Security.Claims;
using Core.AuditLog;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class RemoveMember
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapDelete("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .WithSummary("Remove member from item group")
            .WithDescription(
                "Revokes a user's access to an item group by removing them as a member. The authenticated user must be a member of the group. Returns 409 Conflict if the target member is the last member of the group, as removing them would leave the group permanently unreachable."
            )
            .WithTags("Member")
            .WithName(nameof(RemoveMember));
    }

    public static async Task<
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
    > Execute(
        Guid itemGroupId,
        Guid memberId,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        AuditContext auditContext,
        IChecklistNotifier notifier,
        HttpRequest httpRequest,
        CancellationToken ct
    )
    {
        auditContext.TargetUserId = memberId;

        return await db.ExecuteAsItemGroupMember<
            Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
        >(
            itemGroupId,
            claimsPrincipal,
            async _ =>
            {
                if (!await RemoveData(itemGroupId, memberId, db, ct))
                {
                    return TypedResults.Conflict();
                }

                string? connectionId = httpRequest.Headers["X-SignalR-Connection-Id"];
                await notifier.NotifyMemberRemoved(itemGroupId, memberId, connectionId);
                return TypedResults.NoContent();
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    /// <summary>
    /// Removes the member unless they're the group's last one. Returns <see langword="false"/>,
    /// changing nothing, for the last member, and <see langword="true"/> otherwise, including
    /// when they weren't a member. The check and the delete share a transaction that first
    /// takes an update lock on the group's row, so removals from one group take turns. Without
    /// it, two members removing each other (or themselves) at the same moment could both pass
    /// the check and leave the group with no members.
    /// </summary>
    internal static async Task<bool> RemoveData(
        Guid itemGroupId,
        Guid memberId,
        IDbConnection db,
        CancellationToken ct
    )
    {
        // Dapper opens connections per command, but a transaction needs one that is already open.
        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        using IDbTransaction tx = db.BeginTransaction();

        int isLastMember = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                DECLARE @Locked BIT;
                SELECT @Locked = 1 FROM ItemGroups WITH (UPDLOCK) WHERE Id = @ItemGroupId;

                SELECT CASE
                    WHEN COUNT(*) = 1
                     AND SUM(CASE WHEN MemberId = @MemberId THEN 1 ELSE 0 END) = 1
                    THEN 1 ELSE 0 END
                FROM Members WHERE ItemGroupId = @ItemGroupId;
                """,
                new { ItemGroupId = itemGroupId, MemberId = memberId },
                transaction: tx,
                cancellationToken: ct
            )
        );
        if (isLastMember == 1)
        {
            return false;
        }

        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Members WHERE MemberId = @MemberId AND ItemGroupId = @ItemGroupId",
                new { MemberId = memberId, ItemGroupId = itemGroupId },
                transaction: tx,
                cancellationToken: ct
            )
        );

        tx.Commit();
        return true;
    }
}

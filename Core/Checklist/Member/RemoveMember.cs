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
        CancellationToken ct
    )
    {
        auditContext.TargetUserId = memberId;

        Guid? userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        bool isMember = await db.IsMember(itemGroupId, userId, ct);
        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        bool isLastMember = await db.IsLastMember(itemGroupId, memberId, ct);
        if (isLastMember)
        {
            return TypedResults.Conflict();
        }

        await RemoveData(itemGroupId, memberId, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task RemoveData(
        Guid itemGroupId,
        Guid memberId,
        IDbConnection db,
        CancellationToken ct
    )
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Members WHERE MemberId = @MemberId AND ItemGroupId = @ItemGroupId",
                new { MemberId = memberId, ItemGroupId = itemGroupId },
                cancellationToken: ct
            )
        );
    }
}

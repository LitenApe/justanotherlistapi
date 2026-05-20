using System.Data;
using System.Security.Claims;
using Core.AuditLog;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class AddMember
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .WithSummary("Add a new member to an item group")
            .WithDescription(
                "Grants another user access to an item group by adding them as a member. The authenticated user must already be a member. Returns 409 Conflict if the user is already a member."
            )
            .WithTags("Member")
            .WithName(nameof(AddMember));
    }

    public static async Task<
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict>
    > Execute(
        Guid itemGroupId,
        Guid memberId,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        AuditContext auditContext,
        CancellationToken ct = default
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
                bool alreadyMember = await db.IsMember(itemGroupId, memberId, ct);
                if (alreadyMember)
                {
                    return TypedResults.Conflict();
                }

                await CreateData(itemGroupId, memberId, db, ct);
                return TypedResults.NoContent();
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    internal static async Task CreateData(
        Guid itemGroupId,
        Guid memberId,
        IDbConnection db,
        CancellationToken ct
    )
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
                new { MemberId = memberId, ItemGroupId = itemGroupId },
                cancellationToken: ct
            )
        );
    }
}

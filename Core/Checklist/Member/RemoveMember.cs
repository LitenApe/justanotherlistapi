using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class RemoveMember
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapDelete("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .WithSummary("Remove member from item group")
            .WithDescription("Revokes a user's access to an item group by removing them as a member. The authenticated user must be a member of the group.")
            .WithTags("Member")
            .WithName(nameof(RemoveMember));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Guid memberId,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct)
    {
        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var isMember = await db.IsMember(itemGroupId, userId, ct);
        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        await RemoveData(itemGroupId, memberId, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task RemoveData(Guid itemGroupId, Guid memberId, IDbConnection db, CancellationToken ct)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Members WHERE MemberId = @MemberId AND ItemGroupId = @ItemGroupId",
            new { MemberId = memberId, ItemGroupId = itemGroupId },
            cancellationToken: ct));
    }
}

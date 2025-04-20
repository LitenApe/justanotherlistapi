using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class RemoveMember
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapDelete("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .WithSummary("Remove member from item group")
            .WithTags(nameof(Member))
            .WithName(nameof(RemoveMember));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Guid memberId,
        ClaimsPrincipal claimsPrincipal,
        DatabaseContext db,
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

    internal static async Task RemoveData(Guid itemGroupId, Guid memberId, DatabaseContext db, CancellationToken ct)
    {
        var member = new Member { ItemGroupId = itemGroupId, MemberId = memberId.ToString() };
        db.Members.Remove(member);
        await db.SaveChangesAsync(ct);
    }
}

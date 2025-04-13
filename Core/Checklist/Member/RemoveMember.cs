using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JustAnotherListApi.Checklist;

public static class RemoveMember
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapDelete("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .RequireAuthorization()
            .WithSummary("Remove member from item group")
            .WithTags(nameof(Member))
            .WithName(nameof(RemoveMember));
        return app;
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

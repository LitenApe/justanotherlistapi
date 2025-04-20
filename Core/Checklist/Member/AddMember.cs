using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class AddMember
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapPost("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .WithSummary("Add a new member to a item group")
            .WithTags(nameof(Member))
            .WithName(nameof(AddMember));
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

        await CreateData(itemGroupId, memberId, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task CreateData(Guid itemGroupId, Guid memberId, DatabaseContext db, CancellationToken ct)
    {
        var member = new Member
        {
            ItemGroupId = itemGroupId,
            MemberId = memberId.ToString()
        };
        db.Members.Add(member);
        await db.SaveChangesAsync(ct);
    }
}

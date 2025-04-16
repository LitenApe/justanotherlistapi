using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetMembers
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{itemGroupId:guid}/member", Execute)
            .WithSummary("Get members of a item group")
            .WithTags(nameof(Member))
            .WithName(nameof(GetMembers));
    }

    public static async Task<Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
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

        var data = await LoadData(itemGroupId, db, ct);
        if (data is null)
        {
            return TypedResults.Ok(new List<string> { userId });
        }

        return TypedResults.Ok(data);
    }

    internal static async Task<List<string>?> LoadData(Guid itemGroupId, DatabaseContext db, CancellationToken ct)
    {
        return await db.Members
            .AsNoTracking()
            .Where(m => m.ItemGroupId == itemGroupId)
            .Select(m => m.MemberId)
            .ToListAsync(ct);
    }
}

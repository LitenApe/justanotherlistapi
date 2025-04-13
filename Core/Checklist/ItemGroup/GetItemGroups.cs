using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetItemGroups
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapGet("/", Execute)
            .RequireAuthorization()
            .WithSummary("Get all item groups related to authenticated user")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroups));
        return app;
    }

    public static async Task<Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult>> Execute(ClaimsPrincipal claimsPrincipal, DatabaseContext db, CancellationToken ct)
    {
        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var itemGroups = await LoadData(userId, db, ct);
        if (itemGroups is null)
        {
            return TypedResults.Ok(new List<ItemGroup>());
        }

        return TypedResults.Ok(itemGroups);
    }

    internal static async Task<List<ItemGroup>> LoadData(string userId, DatabaseContext db, CancellationToken ct)
    {
        var memberDb = db.Members.AsNoTracking();
        var itemGroupDb = db.ItemGroups.AsNoTracking();

        return await memberDb
            .Where(
            m => m.MemberId == userId)
            .Join(itemGroupDb.Include(ig => ig.Items.Where(i => !i.IsComplete)),
            ig => ig.ItemGroupId,
            m => m.Id,
            (m, ig) => ig)
            .ToListAsync(ct);
    }
}

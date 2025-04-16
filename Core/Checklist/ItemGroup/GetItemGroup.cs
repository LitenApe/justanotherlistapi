using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetItemGroup
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{itemGroupId:guid}", Execute)
            .WithSummary("Get a item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroup));
    }

    public static async Task<Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
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

        var itemGroup = await LoadData(itemGroupId, db, ct);
        if (itemGroup is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(itemGroup);
    }

    internal static Task<ItemGroup?> LoadData(Guid itemGroupId, DatabaseContext db, CancellationToken ct)
    {
        return db.ItemGroups
            .AsNoTracking()
            .Include(ig => ig.Items)
            .Include(ig => ig.Members)
            .FirstOrDefaultAsync(ig => ig.Id == itemGroupId, ct);
    }
}

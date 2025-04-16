using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JustAnotherListApi.Checklist;

public static class DeleteItemGroup
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapDelete("/{itemGroupId:guid}", Execute)
            .WithSummary("Delete a item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(DeleteItemGroup));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        ClaimsPrincipal claimsPrincipal,
        DatabaseContext db,
        CancellationToken ct = default)
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

        await RemoveData(itemGroupId, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task RemoveData(Guid itemGroupId, DatabaseContext db, CancellationToken ct)
    {
        var itemGroup = await db.ItemGroups.FindAsync([itemGroupId], cancellationToken: ct);
        if (itemGroup is null)
        {
            return;
        }

        db.ItemGroups.Remove(itemGroup);
        await db.SaveChangesAsync(ct);
    }
}

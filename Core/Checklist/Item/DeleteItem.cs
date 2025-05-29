using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Core.Checklist;
public static class DeleteItem
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapDelete("/{itemGroupId:guid}/{itemId:guid}", Execute)
            .WithSummary("Delete a item")
            .WithTags(nameof(Item))
            .WithName(nameof(DeleteItem));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Guid itemId,
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

        await DeleteData(itemGroupId, itemId, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task DeleteData(Guid itemGroupId, Guid itemId, DatabaseContext db, CancellationToken ct)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId && i.ItemGroupId == itemGroupId, cancellationToken: ct);
        if (item is null)
        {
            return;
        }

        db.Items.Remove(item);
        await db.SaveChangesAsync(ct);
    }
}

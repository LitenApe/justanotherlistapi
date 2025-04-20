using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;
public static class UpdateItem
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapPut("/{itemGroupId:guid}/{itemId:guid}", Execute)
            .WithSummary("Update a item")
            .WithTags(nameof(Item))
            .WithName(nameof(UpdateItem));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Guid itemId,
        Request request,
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

        await UpdateData(itemId, request, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task UpdateData(Guid itemId, Request request, DatabaseContext db, CancellationToken ct)
    {
        var item = await db.Items.FindAsync([itemId], cancellationToken: ct);
        if (item is null)
        {
            return;
        }

        item.Name = request.Name;
        item.Description = request.Description;
        item.IsComplete = request.IsComplete;

        db.Update(item);
        await db.SaveChangesAsync(ct);
    }

    public class Request
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        [DefaultValue(false)]
        public bool IsComplete { get; set; }
    }
}
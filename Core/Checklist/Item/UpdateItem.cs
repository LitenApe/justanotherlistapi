using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

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

    public static async Task<Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Guid itemId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        DatabaseContext db,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Name.Trim()))
        {
            return TypedResults.BadRequest();
        }

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

        await UpdateData(itemGroupId, itemId, request, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task UpdateData(Guid itemGroupId, Guid itemId, Request request, DatabaseContext db, CancellationToken ct)
    {
        var item = await db.Items.FirstOrDefaultAsync((i) => i.Id == itemId && i.ItemGroupId == itemGroupId, cancellationToken: ct);
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
        [Description("Name of the item")]
        public required string Name { get; set; }

        [Description("Description of the item")]
        public string? Description { get; set; }

        [DefaultValue(false)]
        [Description("Indicates if the item is complete")]
        public bool IsComplete { get; set; }
    }
}
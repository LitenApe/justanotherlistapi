using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace JustAnotherListApi.Checklist;
public static class UpdateItem
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapPut("/{itemGroupId}/{itemId}", Execute)
            .RequireAuthorization()
            .WithSummary("Update a item")
            .WithTags(nameof(Item))
            .WithName(nameof(UpdateItem));
        return app;
    }

    public class Request
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        [DefaultValue(false)]
        public bool IsComplete { get; set; }
    }

    public static async Task<IResult> Execute(Guid itemGroupId, Guid itemId, Request updatedItem, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

        var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        var item = await db.Items.FindAsync(itemId);

        if (item is null)
        {
            return TypedResults.BadRequest();
        }

        item.Name = updatedItem.Name;
        item.Description = updatedItem.Description;
        item.IsComplete = updatedItem.IsComplete;

        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class CreateItem
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapPost("/{itemGroupId}", Execute)
            .RequireAuthorization()
            .WithSummary("Create a item")
            .WithTags(nameof(Item))
            .WithName(nameof(CreateItem));
        return app;
    }

    public class Request
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        [DefaultValue(false)]
        public bool IsComplete { get; set; }

        public static Item toItem(Guid itemGroupId, Request item)
        {
            return new Item()
            {
                ItemGroupId = itemGroupId,
                Name = item.Name,
                Description = item.Description,
                IsComplete = item.IsComplete,
            };
        }
    }

    public static async Task<IResult> Execute(Guid itemGroupId, Request newItem, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

        var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        Item item = Request.toItem(itemGroupId, newItem);

        await db.Items.AddAsync(item);

        await db.SaveChangesAsync();

        return TypedResults.Created($"/list/{itemGroupId}/{item.Id}", item);
    }
}

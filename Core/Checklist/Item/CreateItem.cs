using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class CreateItem
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapPost("/{itemGroupId:guid}", Execute)
            .WithSummary("Create a item")
            .WithTags(nameof(Item))
            .WithName(nameof(CreateItem));
    }

    public static async Task<Results<Created<Item>, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
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

        var data = await CreateData(itemGroupId, request, db, ct);
        return TypedResults.Created($"/list/{itemGroupId}/{data.Id}", data);
    }

    internal static async Task<Item> CreateData(Guid itemGroupId, Request request, DatabaseContext db, CancellationToken ct)
    {
        var item = new Item
        {
            ItemGroupId = itemGroupId,
            Name = request.Name,
            Description = request.Description,
            IsComplete = request.IsComplete
        };
        await db.Items.AddAsync(item, ct);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public class Request
    {
        [Description("Name of the item")]
        public required string Name { get; set; }

        [Description("Description of the item")]
        public string? Description { get; set; }

        [DefaultValue(false)]
        [Description("Indicates whether the item is complete")]
        public bool IsComplete { get; set; }
    }
}

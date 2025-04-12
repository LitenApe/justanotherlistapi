using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JustAnotherListApi.Checklist;

public static class CreateItem
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapPost("/{itemGroupId:guid}", Execute)
            .RequireAuthorization()
            .WithSummary("Create a item")
            .WithTags(nameof(Item))
            .WithName(nameof(CreateItem));
        return app;
    }

    public static async Task<Results<Created<Item>, UnauthorizedHttpResult, ForbidHttpResult>> Execute(Guid itemGroupId, Request request, ClaimsPrincipal claimsPrincipal, DatabaseContext db)
    {
        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var isMember = await db.IsMember(itemGroupId, userId);
        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        var data = await CreateData(itemGroupId, request, db);
        return TypedResults.Created($"/list/{itemGroupId}/{data.Id}", data);
    }

    internal static async Task<Item> CreateData(Guid itemGroupId, Request request, DatabaseContext db)
    {
        var item = new Item { ItemGroupId = itemGroupId, Name = request.Name, Description = request.Description, IsComplete = request.IsComplete };
        await db.Items.AddAsync(item);
        await db.SaveChangesAsync();
        return item;
    }

    public class Request
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        [DefaultValue(false)]
        public bool IsComplete { get; set; }
    }
}

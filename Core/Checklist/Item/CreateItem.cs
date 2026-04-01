using System.ComponentModel;
using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class CreateItem
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/{itemGroupId:guid}", Execute)
            .WithSummary("Create an item")
            .WithDescription(
                "Creates a new item within the specified item group. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(Item))
            .WithName(nameof(CreateItem));
    }

    public static async Task<
        Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Execute(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest();
        }

        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        bool isMember = await db.IsMember(itemGroupId, userId, ct);
        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        var data = await CreateData(itemGroupId, request, db, ct);
        return TypedResults.Created($"/list/{itemGroupId}/{data.Id}", data);
    }

    internal static async Task<Item> CreateData(
        Guid itemGroupId,
        Request request,
        IDbConnection db,
        CancellationToken ct
    )
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            ItemGroupId = itemGroupId,
            Name = request.Name,
            Description = request.Description,
            IsComplete = request.IsComplete,
        };

        await db.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
                new
                {
                    item.Id,
                    item.Name,
                    item.Description,
                    item.IsComplete,
                    item.ItemGroupId,
                },
                cancellationToken: ct
            )
        );

        return item;
    }

    public record Request
    {
        [Description("Name of the item")]
        public required string Name { get; init; }

        [Description("Description of the item")]
        public string? Description { get; init; }

        [DefaultValue(false)]
        [Description("Indicates whether the item is complete")]
        public bool IsComplete { get; init; }
    }
}

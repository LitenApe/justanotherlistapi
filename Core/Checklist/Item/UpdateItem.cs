using System.ComponentModel;
using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class UpdateItem
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("/{itemGroupId:guid}/{itemId:guid}", Execute)
            .WithSummary("Update an item")
            .WithDescription(
                "Updates the name, description, or completion status of an existing item. The authenticated user must be a member of the item group."
            )
            .WithTags(nameof(Item))
            .WithName(nameof(UpdateItem));
    }

    public static async Task<
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Execute(
        Guid itemGroupId,
        Guid itemId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        IChecklistNotifier notifier,
        HttpRequest httpRequest,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest();
        }

        return await db.ExecuteAsItemGroupMember<
            Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
        >(
            itemGroupId,
            claimsPrincipal,
            async _ =>
            {
                await UpdateData(itemGroupId, itemId, request, db, ct);
                var item = new Item
                {
                    Id = itemId,
                    ItemGroupId = itemGroupId,
                    Name = request.Name,
                    Description = request.Description,
                    IsComplete = request.IsComplete,
                };
                string? connectionId = httpRequest.Headers["X-SignalR-Connection-Id"];
                await notifier.NotifyItemUpdated(itemGroupId, item, connectionId);
                return TypedResults.NoContent();
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    internal static async Task UpdateData(
        Guid itemGroupId,
        Guid itemId,
        Request request,
        IDbConnection db,
        CancellationToken ct
    )
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Items SET Name = @Name, Description = @Description, IsComplete = @IsComplete WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
                new
                {
                    Id = itemId,
                    ItemGroupId = itemGroupId,
                    request.Name,
                    request.Description,
                    request.IsComplete,
                },
                cancellationToken: ct
            )
        );
    }

    public record Request
    {
        [Description("Name of the item")]
        public required string Name { get; init; }

        [Description("Description of the item")]
        public string? Description { get; init; }

        [DefaultValue(false)]
        [Description("Indicates if the item is complete")]
        public bool IsComplete { get; init; }
    }
}

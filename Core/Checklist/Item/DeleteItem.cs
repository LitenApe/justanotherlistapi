using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class DeleteItem
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapDelete("/{itemGroupId:guid}/{itemId:guid}", Execute)
            .WithSummary("Delete an item")
            .WithDescription(
                "Permanently deletes an item from the specified item group. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(Item))
            .WithName(nameof(DeleteItem));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Guid itemId,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default
    )
    {
        return await db.ExecuteAsItemGroupMember<
            Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>
        >(
            itemGroupId,
            claimsPrincipal,
            async _ =>
            {
                await DeleteData(itemGroupId, itemId, db, ct);
                return TypedResults.NoContent();
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    internal static async Task DeleteData(
        Guid itemGroupId,
        Guid itemId,
        IDbConnection db,
        CancellationToken ct
    )
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
                new { Id = itemId, ItemGroupId = itemGroupId },
                cancellationToken: ct
            )
        );
    }
}

using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

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
        IDbConnection db,
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

    internal static async Task DeleteData(Guid itemGroupId, Guid itemId, IDbConnection db, CancellationToken ct)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
            new { Id = itemId, ItemGroupId = itemGroupId },
            cancellationToken: ct));
    }
}

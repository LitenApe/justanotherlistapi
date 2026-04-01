using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class DeleteItemGroup
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapDelete("/{itemGroupId:guid}", Execute)
            .WithSummary("Delete a item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(DeleteItemGroup));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
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

        await RemoveData(itemGroupId, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task RemoveData(Guid itemGroupId, IDbConnection db, CancellationToken ct)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ItemGroups WHERE Id = @Id",
            new { Id = itemGroupId },
            cancellationToken: ct));
    }
}

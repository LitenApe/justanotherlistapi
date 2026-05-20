using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class DeleteItemGroup
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapDelete("/{itemGroupId:guid}", Execute)
            .WithSummary("Delete an item group")
            .WithDescription(
                "Permanently deletes an item group and all its associated items and members. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(DeleteItemGroup));
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
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
                await RemoveData(itemGroupId, db, ct);
                return TypedResults.NoContent();
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    internal static async Task RemoveData(Guid itemGroupId, IDbConnection db, CancellationToken ct)
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM ItemGroups WHERE Id = @Id",
                new { Id = itemGroupId },
                cancellationToken: ct
            )
        );
    }
}

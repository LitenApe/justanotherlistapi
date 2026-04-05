using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class GetItemGroups
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapGet("/", Execute)
            .WithSummary("Get all item groups related to authenticated user with uncompleted items")
            .WithDescription(
                "Returns all item groups where the authenticated user is a member, each populated with their incomplete items only."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroups));
    }

    public static async Task<Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult>> Execute(
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default
    )
    {
        Guid? userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        List<ItemGroup> itemGroups = await LoadData(userId.Value, db, ct);
        return TypedResults.Ok(itemGroups);
    }

    internal static async Task<List<ItemGroup>> LoadData(
        Guid userId,
        IDbConnection db,
        CancellationToken ct
    )
    {
        using SqlMapper.GridReader multi = await db.QueryMultipleAsync(
            new CommandDefinition(
                """
                SELECT ig.Id, ig.Name
                FROM ItemGroups ig
                INNER JOIN Members m ON m.ItemGroupId = ig.Id
                WHERE m.MemberId = @UserId;

                SELECT i.Id, i.Name, i.Description, i.IsComplete, i.ItemGroupId
                FROM Items i
                INNER JOIN Members m ON m.ItemGroupId = i.ItemGroupId
                WHERE m.MemberId = @UserId AND i.IsComplete = 0;
                """,
                new { UserId = userId },
                cancellationToken: ct
            )
        );

        var groups = (await multi.ReadAsync<ItemGroup>()).ToList();
        if (groups.Count == 0)
        {
            return groups;
        }

        var items = (await multi.ReadAsync<Item>()).ToList();

        return groups
            .Select(group =>
                group with
                {
                    Items = items.Where(i => i.ItemGroupId == group.Id).ToList(),
                }
            )
            .ToList();
    }
}

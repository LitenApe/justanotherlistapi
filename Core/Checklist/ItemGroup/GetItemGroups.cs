using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class GetItemGroups
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapGet("/", Execute)
            .WithSummary("Get all item groups related to authenticated user")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroups));
    }

    public static async Task<Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult>> Execute(
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default)
    {
        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var itemGroups = await LoadData(userId.Value, db, ct);
        return TypedResults.Ok(itemGroups);
    }

    internal static async Task<List<ItemGroup>> LoadData(Guid userId, IDbConnection db, CancellationToken ct)
    {
        var groups = (await db.QueryAsync<ItemGroup>(new CommandDefinition(
            """
            SELECT ig.Id, ig.Name
            FROM ItemGroups ig
            INNER JOIN Members m ON m.ItemGroupId = ig.Id
            WHERE m.MemberId = @UserId
            """,
            new { UserId = userId },
            cancellationToken: ct))).ToList();

        if (groups.Count == 0) return groups;

        var groupIds = groups.Select(g => g.Id).ToList();
        var items = (await db.QueryAsync<Item>(new CommandDefinition(
            "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE ItemGroupId IN @GroupIds AND IsComplete = 0",
            new { GroupIds = groupIds },
            cancellationToken: ct))).ToList();

        return groups.Select(group => group with
        {
            Items = items.Where(i => i.ItemGroupId == group.Id).ToList()
        }).ToList();
    }
}

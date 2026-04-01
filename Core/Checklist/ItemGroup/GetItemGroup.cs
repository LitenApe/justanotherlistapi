using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class GetItemGroup
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{itemGroupId:guid}", Execute)
            .WithSummary("Get an item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroup));
    }

    public static async Task<Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
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

        var itemGroup = await LoadData(itemGroupId, db, ct);
        if (itemGroup is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(itemGroup);
    }

    internal static async Task<ItemGroup?> LoadData(Guid itemGroupId, IDbConnection db, CancellationToken ct)
    {
        var itemGroup = await db.QueryFirstOrDefaultAsync<ItemGroup>(new CommandDefinition(
            "SELECT Id, Name FROM ItemGroups WHERE Id = @Id",
            new { Id = itemGroupId },
            cancellationToken: ct));

        if (itemGroup is null) return null;

        var items = await db.QueryAsync<Item>(new CommandDefinition(
            "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId },
            cancellationToken: ct));

        var members = await db.QueryAsync<Member>(new CommandDefinition(
            "SELECT MemberId, ItemGroupId FROM Members WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId },
            cancellationToken: ct));

        return itemGroup with
        {
            Items = items.ToList(),
            Members = members.ToList()
        };
    }
}

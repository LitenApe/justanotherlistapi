using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class GetItemGroup
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapGet("/{itemGroupId:guid}", Execute)
            .WithSummary("Get an item group")
            .WithDescription(
                "Returns a single item group by ID, including all its items and members. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroup));
    }

    public static async Task<
        Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>
    > Execute(
        Guid itemGroupId,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default
    )
    {
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

        var itemGroup = await LoadData(itemGroupId, db, ct);
        if (itemGroup is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(itemGroup);
    }

    internal static async Task<ItemGroup?> LoadData(
        Guid itemGroupId,
        IDbConnection db,
        CancellationToken ct
    )
    {
        using var multi = await db.QueryMultipleAsync(
            new CommandDefinition(
                """
                SELECT Id, Name FROM ItemGroups WHERE Id = @Id;
                SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE ItemGroupId = @Id;
                SELECT MemberId FROM Members WHERE ItemGroupId = @Id;
                """,
                new { Id = itemGroupId },
                cancellationToken: ct
            )
        );

        var itemGroup = await multi.ReadFirstOrDefaultAsync<ItemGroup>();
        if (itemGroup is null)
        {
            return null;
        }

        var items = await multi.ReadAsync<Item>();
        var members = await multi.ReadAsync<Guid>();

        return itemGroup with
        {
            Items = items.ToList(),
            Members = members.ToList(),
        };
    }
}

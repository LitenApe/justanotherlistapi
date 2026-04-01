using System.ComponentModel;
using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class CreateItemGroup
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapPost("/", Execute)
            .WithSummary("Create a new item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(CreateItemGroup));
    }

    public static async Task<Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult>> Execute(
        Request request,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default)
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

        var itemGroup = await CreateData(userId.Value, request, db, ct);
        return TypedResults.Created($"/list/{itemGroup.Id}", itemGroup);
    }

    internal static async Task<ItemGroup> CreateData(Guid userId, Request request, IDbConnection db, CancellationToken ct)
    {
        var itemGroup = new ItemGroup { Id = Guid.NewGuid(), Name = request.Name };

        using var tx = db.BeginTransaction();

        await db.ExecuteAsync(new CommandDefinition(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { itemGroup.Id, itemGroup.Name },
            transaction: tx,
            cancellationToken: ct));

        await db.ExecuteAsync(new CommandDefinition(
            "INSERT INTO Members (ItemGroupId, MemberId) VALUES (@ItemGroupId, @MemberId)",
            new { ItemGroupId = itemGroup.Id, MemberId = userId },
            transaction: tx,
            cancellationToken: ct));

        tx.Commit();

        return itemGroup with
        {
            Members = [new Member { MemberId = userId, ItemGroupId = itemGroup.Id }]
        };
    }

    public record Request
    {
        [Description("Name of the item group")]
        public required string Name { get; init; }
    }
}

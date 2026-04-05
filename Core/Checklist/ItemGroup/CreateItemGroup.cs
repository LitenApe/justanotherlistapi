using System.ComponentModel;
using System.Data;
using System.Security.Claims;
using Core.AuditLog;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class CreateItemGroup
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/", Execute)
            .WithSummary("Create a new item group")
            .WithDescription(
                "Creates a new item group and automatically adds the authenticated user as its first member."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(CreateItemGroup));
    }

    public static async Task<
        Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult>
    > Execute(
        Request request,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        AuditContext auditContext,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest();
        }

        Guid? userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        ItemGroup itemGroup = await CreateData(userId.Value, request, db, ct);
        auditContext.ResourceId = itemGroup.Id;
        return TypedResults.Created($"/list/{itemGroup.Id}", itemGroup);
    }

    internal static async Task<ItemGroup> CreateData(
        Guid userId,
        Request request,
        IDbConnection db,
        CancellationToken ct
    )
    {
        var itemGroup = new ItemGroup { Id = Guid.NewGuid(), Name = request.Name };

        using IDbTransaction tx = db.BeginTransaction();

        await db.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name);
                INSERT INTO Members (ItemGroupId, MemberId) VALUES (@Id, @MemberId);
                """,
                new
                {
                    itemGroup.Id,
                    itemGroup.Name,
                    MemberId = userId,
                },
                transaction: tx,
                cancellationToken: ct
            )
        );

        tx.Commit();

        return itemGroup with
        {
            Members = [userId],
        };
    }

    public record Request
    {
        [Description("Name of the item group")]
        public required string Name { get; init; }
    }
}

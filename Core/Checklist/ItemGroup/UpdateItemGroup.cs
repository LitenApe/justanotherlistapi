using System.ComponentModel;
using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class UpdateItemGroup
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPut("/{itemGroupId:guid}", Execute)
            .WithSummary("Update an item group")
            .WithDescription(
                "Updates the name of an existing item group. The authenticated user must be a member of the group."
            )
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(UpdateItemGroup));
    }

    public static async Task<
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
    > Execute(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest();
        }

        return await db.ExecuteAsItemGroupMember<
            Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>
        >(
            itemGroupId,
            claimsPrincipal,
            async _ =>
            {
                await UpdateData(itemGroupId, request, db, ct);
                return TypedResults.NoContent();
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    internal static async Task UpdateData(
        Guid itemGroupId,
        Request request,
        IDbConnection db,
        CancellationToken ct
    )
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "UPDATE ItemGroups SET Name = @Name WHERE Id = @Id",
                new { Id = itemGroupId, Name = request.Name },
                cancellationToken: ct
            )
        );
    }

    public record Request
    {
        [Description("Name of the item group")]
        public required string Name { get; init; }
    }
}

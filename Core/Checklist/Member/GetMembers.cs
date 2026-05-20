using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class GetMembers
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder)
    {
        builder
            .MapGet("/{itemGroupId:guid}/member", Execute)
            .WithSummary("Get members of an item group")
            .WithDescription(
                "Returns the list of user IDs that are members of the specified item group. The authenticated user must already be a member."
            )
            .WithTags("Member")
            .WithName(nameof(GetMembers));
    }

    public static async Task<
        Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>
    > Execute(
        Guid itemGroupId,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct
    )
    {
        return await db.ExecuteAsItemGroupMember<
            Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>
        >(
            itemGroupId,
            claimsPrincipal,
            async _ =>
            {
                List<Guid> data = await LoadData(itemGroupId, db, ct);
                return TypedResults.Ok(data);
            },
            TypedResults.Unauthorized(),
            TypedResults.Forbid(),
            ct
        );
    }

    internal static async Task<List<Guid>> LoadData(
        Guid itemGroupId,
        IDbConnection db,
        CancellationToken ct
    )
    {
        IEnumerable<Guid> result = await db.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT MemberId FROM Members WHERE ItemGroupId = @ItemGroupId",
                new { ItemGroupId = itemGroupId },
                cancellationToken: ct
            )
        );
        return [.. result];
    }
}

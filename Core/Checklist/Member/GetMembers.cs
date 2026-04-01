using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Checklist;

public static class GetMembers
{
    public static void MapEndpoint(this RouteGroupBuilder builder)
    {
        builder.MapGet("/{itemGroupId:guid}/member", Execute)
            .WithSummary("Get members of an item group")
            .WithTags(nameof(Member))
            .WithName(nameof(GetMembers));
    }

    public static async Task<Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct)
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

        var data = await LoadData(itemGroupId, db, ct);
        return TypedResults.Ok(data);
    }

    internal static async Task<List<Guid>> LoadData(Guid itemGroupId, IDbConnection db, CancellationToken ct)
    {
        var result = await db.QueryAsync<Guid>(new CommandDefinition(
            "SELECT MemberId FROM Members WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId },
            cancellationToken: ct));
        return result.ToList();
    }
}

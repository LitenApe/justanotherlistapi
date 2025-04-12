using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetItemGroup
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapGet("/{itemGroupId:guid}", Execute)
            .RequireAuthorization()
            .WithSummary("Get a item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroup));
        return app;
    }

    public static async Task<Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> Execute(Guid itemGroupId, ClaimsPrincipal claimsPrincipal, DatabaseContext db)
    {
        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var isMember = await db.IsMember(itemGroupId, userId);
        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        var itemGroup = await LoadData(itemGroupId, db);
        if (itemGroup is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(itemGroup);
    }

    internal static Task<ItemGroup> LoadData(Guid itemGroupId, DatabaseContext db)
    {
        return db.ItemGroups
          .Include(ig => ig.Items)
          .Include(ig => ig.Members)
          .FirstAsync(ig => ig.Id == itemGroupId);
    }
}

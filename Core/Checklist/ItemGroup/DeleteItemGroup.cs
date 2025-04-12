using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class DeleteItemGroup
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapDelete("/{itemGroupId:guid}", Execute)
            .RequireAuthorization()
            .WithSummary("Delete a item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(DeleteItemGroup));
        return app;
    }

    public static async Task<IResult> Execute(Guid itemGroupId, ClaimsPrincipal claimsPrincipal, DatabaseContext db)
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

        await RemoveData(itemGroupId, db);
        return TypedResults.NoContent();
    }

    internal static async Task RemoveData(Guid itemGroupId, DatabaseContext db)
    {
        var itemGroup = await db.ItemGroups.FindAsync(itemGroupId);

        if (itemGroup is null)
        {
            return;
        }

        db.ItemGroups.Remove(itemGroup);
        await db.SaveChangesAsync();
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JustAnotherListApi.Checklist;
public static class DeleteItem
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapDelete("/{itemGroupId:guid}/{itemId:guid}", Execute)
            .RequireAuthorization()
            .WithSummary("Delete a item")
            .WithTags(nameof(Item))
            .WithName(nameof(DeleteItem));
        return app;
    }
    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(Guid itemGroupId, Guid itemId, ClaimsPrincipal claimsPrincipal, DatabaseContext db)
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

        await DeleteData(itemId, db);
        return TypedResults.NoContent();
    }

    internal static async Task DeleteData(Guid itemId, DatabaseContext db)
    {
        var item = await db.Items.FindAsync(itemId);
        if (item is null)
        {
            return;
        }

        db.Items.Remove(item);
        await db.SaveChangesAsync();
    }
}

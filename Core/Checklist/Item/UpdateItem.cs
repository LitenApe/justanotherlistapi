using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JustAnotherListApi.Checklist;
public static class UpdateItem
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapPut("/{itemGroupId:guid}/{itemId:guid}", Execute)
            .RequireAuthorization()
            .WithSummary("Update a item")
            .WithTags(nameof(Item))
            .WithName(nameof(UpdateItem));
        return app;
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Guid itemId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        DatabaseContext db,
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

        await UpdateData(itemId, request, db, ct);
        return TypedResults.NoContent();
    }

    internal static async Task UpdateData(Guid itemId, Request request, DatabaseContext db, CancellationToken ct)
    {
        var item = await db.Items.FindAsync(itemId, ct);
        if (item is null)
        {
            return;
        }

        item.Name = request.Name;
        item.Description = request.Description;
        item.IsComplete = request.IsComplete;

        db.Update(item);
        await db.SaveChangesAsync(ct);
    }

    public class Request
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        [DefaultValue(false)]
        public bool IsComplete { get; set; }
    }
}
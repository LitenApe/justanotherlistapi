using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;
public static class DeleteItem
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapDelete("/{itemGroupId}/{itemId}", Execute)
            .WithTags(nameof(Item))
            .WithName(nameof(DeleteItem));
        return app;
    }
    public static async Task<IResult> Execute(Guid itemGroupId, Guid itemId, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

        var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        var item = await db.Items.FindAsync(itemId);

        if (item is null)
        {
            return TypedResults.BadRequest();
        }

        db.Items.Remove(item);

        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}

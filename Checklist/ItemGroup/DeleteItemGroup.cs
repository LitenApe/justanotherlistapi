using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class DeleteItemGroup
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapDelete("/{itemGroupId}", Execute)
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(DeleteItemGroup));
        return app;
    }

    public static async Task<IResult> Execute(Guid itemGroupId, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
        var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        var itemGroup = await db.ItemGroups.FindAsync(itemGroupId);

        if (itemGroup is null)
        {
            return TypedResults.BadRequest();
        }

        db.ItemGroups.Remove(itemGroup);

        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}

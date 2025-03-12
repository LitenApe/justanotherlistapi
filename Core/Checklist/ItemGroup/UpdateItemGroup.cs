using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class UpdateItemGroup
{
    public class Request
    {
        public required string Name { get; set; }
    }

    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapPut("/{itemGroupId}", Execute)
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(UpdateItemGroup));
        return app;
    }

    public static async Task<IResult> Execute(Guid itemGroupId, Request request, DatabaseContext db)
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

        itemGroup.Name = request.Name;

        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}

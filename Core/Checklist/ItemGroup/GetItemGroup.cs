using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetItemGroup
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapGet("/{itemGroupId}", Execute)
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroup));
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

        var itemGroup = await db.ItemGroups
          .Include(ig => ig.Items)
          .Include(ig => ig.Members)
          .FirstAsync(ig => ig.Id == itemGroupId);

        return TypedResults.Ok(itemGroup);
    }
}

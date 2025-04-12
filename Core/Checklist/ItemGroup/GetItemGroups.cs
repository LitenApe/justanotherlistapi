using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetItemGroups
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapGet("/", Execute)
            .RequireAuthorization()
            .WithSummary("Get all item groups related to authenticated user")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(GetItemGroups));
        return app;
    }

    public static async Task<IResult> Execute(DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

        var memberDb = db.Members;
        var itemGroupDb = db.ItemGroups;

        var lists = await memberDb.Where(m => m.MemberId == userId)
          .Join(
            itemGroupDb.Include(ig => ig.Items.Where(i => !i.IsComplete)),
            ig => ig.ItemGroupId,
            m => m.Id,
            (m, ig) => ig
          )
          .ToListAsync();

        return TypedResults.Ok(lists);
    }
}

using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetMembers
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapGet("/{itemGroupId:guid}/member", Execute)
            .WithTags(nameof(Member))
            .WithName(nameof(GetMembers));
        return app;
    }

    public static async Task<IResult> Execute(Guid itemGroupId, DatabaseContext db)
    {
        var data = await LoadData(itemGroupId, db);

        if (data is null)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(data);
    }

    public static async Task<IEnumerable<Guid>?> LoadData(Guid itemGroupId, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
        var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

        if (isMember)
        {
            return null;
        }

        return db.Members
            .AsNoTracking()
            .Where(m => m.ItemGroupId == itemGroupId)
            .Select(m => m.MemberId);
    }
}

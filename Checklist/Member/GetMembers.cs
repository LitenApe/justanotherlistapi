using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class GetMembers
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapGet("/{itemGroupId}/member", Execute)
            .WithTags(nameof(Member))
            .WithName(nameof(GetMembers));
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

        var members = await db.Members
          .Where(m => m.ItemGroupId == itemGroupId)
          .Select(m => m.MemberId)
          .ToListAsync();

        if (members is null)
        {
            return TypedResults.BadRequest();
        }

        return TypedResults.Ok(members);
    }
}

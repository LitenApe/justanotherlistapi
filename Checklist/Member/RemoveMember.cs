using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class RemoveMember
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapDelete("/{itemGroupId}/member/{memberId}", Execute)
            .WithTags(nameof(Member))
            .WithName(nameof(RemoveMember));
        return app;
    }
    
    public static async Task<IResult> Execute(Guid itemGroupId,  Guid memberId, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

        var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        var member = new Member { ItemGroupId = itemGroupId, MemberId = memberId };

        db.Members.Remove(member);

        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}

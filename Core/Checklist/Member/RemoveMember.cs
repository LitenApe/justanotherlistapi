using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class RemoveMember
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapDelete("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .WithTags(nameof(Member))
            .WithName(nameof(RemoveMember));
        return app;
    }

    public static async Task<IResult> Execute(Guid itemGroupId,  Guid memberId, DatabaseContext db)
    {
        var data = await LoadData(itemGroupId, memberId, db);

        if (data is null)
        {
            return TypedResults.Forbid();
        }

        await UpdateData(data, db);
        return TypedResults.NoContent();
    }

    public static async Task<Member?> LoadData(Guid itemGroupId, Guid memberId, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
        var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

        if (isMember)
        {
            return null;
        }

        return await db.Members.FirstAsync(m => m.ItemGroupId.Equals(itemGroupId) && m.MemberId.Equals(memberId));
    }

    public static async Task UpdateData(Member member, DatabaseContext db)
    {
        db.Members.Remove(member);
        await db.SaveChangesAsync();
    }
}

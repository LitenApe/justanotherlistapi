using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi.Checklist;

public static class AddMember
{
    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapPost("/{itemGroupId:guid}/member/{memberId:guid}", Execute)
            .RequireAuthorization()
            .WithSummary("Add a new member to a item group")
            .WithTags(nameof(Member))
            .WithName(nameof(AddMember));
        return app;
    }

    public static async Task<IResult> Execute(Guid itemGroupId, Guid memberId, DatabaseContext db)
    {
        var data = await LoadData(itemGroupId, db);

        if (!data)
        {
            return TypedResults.Forbid();
        }

        await UpdateData(itemGroupId, memberId, db);
        return TypedResults.NoContent();
    }

    public static async Task<bool> LoadData(Guid itemGroupId, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
        return await db.Members.AnyAsync(ig => ig.ItemGroupId.Equals(itemGroupId) && ig.MemberId.Equals(userId));
    }

    public static async Task UpdateData(Guid itemGroupId, Guid memberId, DatabaseContext db)
    {
        var member = new Member { ItemGroupId = itemGroupId, MemberId = memberId };
        db.Members.Add(member);
        await db.SaveChangesAsync();
    }
}

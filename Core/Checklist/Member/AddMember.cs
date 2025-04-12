using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
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

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Execute(Guid itemGroupId, Guid memberId, ClaimsPrincipal claimsPrincipal, DatabaseContext db)
    {
        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var isMember = await db.IsMember(itemGroupId, userId);
        if (!isMember)
        {
            return TypedResults.Forbid();
        }

        await CreateData(itemGroupId, memberId, db);
        return TypedResults.NoContent();
    }

    internal static async Task CreateData(Guid itemGroupId, Guid memberId, DatabaseContext db)
    {
        var member = new Member
        {
            ItemGroupId = itemGroupId,
            MemberId = memberId.ToString()
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
    }
}

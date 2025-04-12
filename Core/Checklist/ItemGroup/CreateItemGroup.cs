using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace JustAnotherListApi.Checklist;

public static class CreateItemGroup
{
    public class Request
    {
        public required string Name { get; set; }
    }

    public static WebApplication MapEndpoint(this WebApplication app)
    {
        app.MapGroup("/api/list")
            .MapPost("/", Execute)
            .RequireAuthorization()
            .WithSummary("Create a new item group")
            .WithTags(nameof(ItemGroup))
            .WithName(nameof(CreateItemGroup));
        return app;
    }

    public static async Task<Created<ItemGroup>> Execute(Request request, ClaimsPrincipal claimsPrincipal, UserManager<IdentityUser> userManager, DatabaseContext db)
    {
        Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

        var itemGroup = new ItemGroup { Name = request.Name };

        db.ItemGroups.Add(itemGroup);

        var member = new Member { ItemGroupId = itemGroup.Id, MemberId = userId };

        db.Members.Add(member);

        await db.SaveChangesAsync();

        return TypedResults.Created($"/list/{itemGroup.Id}", itemGroup);
    }
}

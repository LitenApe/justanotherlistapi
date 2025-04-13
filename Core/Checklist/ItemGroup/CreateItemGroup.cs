using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

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

    public static async Task<Results<Created<ItemGroup>, UnauthorizedHttpResult>> Execute(
        Request request,
        ClaimsPrincipal claimsPrincipal,
        DatabaseContext db,
        CancellationToken ct = default)
    {
        var userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var itemGroup = await CreateData(userId, request, db, ct);
        return TypedResults.Created($"/list/{itemGroup.Id}", itemGroup);
    }

    internal static async Task<ItemGroup> CreateData(string userId, Request request, DatabaseContext db, CancellationToken ct)
    {
        var itemGroup = new ItemGroup { Name = request.Name };
        await db.ItemGroups.AddAsync(itemGroup, ct);

        var member = new Member { ItemGroupId = itemGroup.Id, MemberId = userId };
        await db.Members.AddAsync(member, ct);
        await db.SaveChangesAsync(ct);
        return itemGroup;
    }
}

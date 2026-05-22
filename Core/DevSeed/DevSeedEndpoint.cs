using System.Data;
using Bogus;
using Dapper;

namespace Core.DevSeed;

public static class DevSeedEndpoint
{
    public static void MapDevSeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/dev/seed",
                async (IDbConnection db, HttpContext context) =>
                {
                    Guid? userId = context.User.GetUserId();
                    if (userId is null)
                    {
                        return Results.Unauthorized();
                    }

                    var groupFaker = new Faker<SeedGroup>()
                        .RuleFor(g => g.Id, f => Guid.NewGuid())
                        .RuleFor(g => g.Name, f => f.Commerce.Department() + " " + f.Hacker.Noun());

                    var itemFaker = new Faker<SeedItem>()
                        .RuleFor(i => i.Id, f => Guid.NewGuid())
                        .RuleFor(i => i.Name, f => f.Commerce.ProductName())
                        .RuleFor(
                            i => i.Description,
                            f => f.Random.Bool(0.6f) ? f.Lorem.Sentence() : null
                        )
                        .RuleFor(i => i.IsComplete, f => f.Random.Bool(0.3f));

                    var groups = groupFaker.Generate(10);

                    foreach (var group in groups)
                    {
                        await db.ExecuteAsync(
                            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
                            new { group.Id, group.Name }
                        );

                        await db.ExecuteAsync(
                            "INSERT INTO Members (ItemGroupId, UserId) VALUES (@ItemGroupId, @UserId)",
                            new { ItemGroupId = group.Id, UserId = userId.Value }
                        );

                        int itemCount = Random.Shared.Next(20, 31);
                        var items = itemFaker.Generate(itemCount);

                        foreach (var item in items)
                        {
                            await db.ExecuteAsync(
                                "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
                                new
                                {
                                    item.Id,
                                    item.Name,
                                    item.Description,
                                    IsComplete = item.IsComplete ? 1 : 0,
                                    ItemGroupId = group.Id,
                                }
                            );
                        }
                    }

                    return Results.NoContent();
                }
            )
            .RequireAuthorization()
            .WithTags("Dev");
    }

    private sealed record SeedGroup
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed record SeedItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsComplete { get; set; }
    }
}

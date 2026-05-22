using System.Data;
using System.Security.Claims;
using Bogus;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.DevSeed;

public static class DevSeedEndpoint
{
    private static readonly string[] contexts =
    [
        "Kitchen",
        "Bathroom",
        "Garden",
        "Office",
        "Garage",
        "Bedroom",
        "Living Room",
        "Apartment",
        "Wedding",
        "Birthday",
        "Holiday",
        "Q3",
        "Q4",
        "Sprint",
        "Project",
    ];

    private static readonly string[] types =
    [
        "Renovation",
        "Cleanup",
        "Shopping",
        "Planning",
        "Tasks",
        "Maintenance",
        "Preparation",
        "Checklist",
        "To-Do",
        "Setup",
    ];

    private static readonly string[] verbs =
    [
        "Order",
        "Buy",
        "Call",
        "Schedule",
        "Review",
        "Compare",
        "Fix",
        "Replace",
        "Clean",
        "Organize",
        "Sort",
        "Pack",
        "Ship",
        "Research",
        "Book",
        "Cancel",
        "Renew",
        "Update",
        "Check",
        "Measure",
        "Paint",
        "Install",
        "Remove",
        "Assemble",
        "Return",
        "Send",
        "Print",
        "File",
        "Submit",
        "Prepare",
    ];

    private static readonly string[] objects =
    [
        "cabinet handles",
        "quarterly budget",
        "paint samples",
        "plumber",
        "electrician",
        "insurance policy",
        "flight tickets",
        "hotel room",
        "rental car",
        "moving boxes",
        "cleaning supplies",
        "light fixtures",
        "door handles",
        "curtain rods",
        "storage bins",
        "power tools",
        "garden hose",
        "lawn mower",
        "vacuum filter",
        "air filter",
        "smoke detector",
        "battery backup",
        "drawer organizers",
        "shelf brackets",
        "wall anchors",
        "picture frames",
        "extension cords",
        "surge protector",
        "water filter",
        "dryer vent",
        "gutters",
        "fence panels",
        "deck boards",
        "window screens",
        "door weatherstrip",
        "thermostat",
        "outlet covers",
        "dimmer switch",
        "ceiling fan",
        "towel rack",
    ];

    private static readonly string[] qualifiers =
    [
        "for hallway",
        "for kitchen",
        "for bathroom",
        "about leak",
        "about wiring",
        "before Friday",
        "by end of week",
        "from hardware store",
        "from online store",
        "for master bedroom",
        "for guest room",
        "for backyard",
        "for front porch",
        "for garage",
        "with warranty",
        "with receipt",
        "for inspection",
        "for estimate",
        "before move-in",
        "after delivery",
    ];

    public static void MapDevSeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/seed", Execute)
            .RequireAuthorization()
            .WithSummary("Seed development data")
            .WithDescription(
                "Clears all existing data and creates 10 checklists with 20-30 items each."
            )
            .WithTags("Dev");
    }

    public static async Task<Results<NoContent, UnauthorizedHttpResult>> Execute(
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        CancellationToken ct = default
    )
    {
        return await claimsPrincipal.ExecuteAsAuthenticatedUser<
            Results<NoContent, UnauthorizedHttpResult>
        >(
            async userId =>
            {
                await SeedData(userId, db, ct);
                return TypedResults.NoContent();
            },
            TypedResults.Unauthorized()
        );
    }

    internal static async Task SeedData(Guid userId, IDbConnection db, CancellationToken ct)
    {
        Faker faker = new();

        using IDbTransaction tx = db.BeginTransaction();

        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Items; DELETE FROM Members; DELETE FROM ItemGroups;",
                transaction: tx,
                cancellationToken: ct
            )
        );

        for (int g = 0; g < 20; g++)
        {
            var groupId = Guid.NewGuid();
            string groupName = faker.PickRandom(contexts) + " " + faker.PickRandom(types);

            await db.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
                    new { Id = groupId, Name = groupName },
                    transaction: tx,
                    cancellationToken: ct
                )
            );

            await db.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO Members (ItemGroupId, MemberId) VALUES (@ItemGroupId, @MemberId)",
                    new { ItemGroupId = groupId, MemberId = userId },
                    transaction: tx,
                    cancellationToken: ct
                )
            );

            int itemCount = Random.Shared.Next(200, 2001);
            for (int i = 0; i < itemCount; i++)
            {
                string verb = faker.PickRandom(verbs);
                string obj = faker.PickRandom(objects);
                string itemName = faker.Random.Bool(0.4f)
                    ? $"{verb} {obj} {faker.PickRandom(qualifiers)}"
                    : $"{verb} {obj}";

                await db.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
                        new
                        {
                            Id = Guid.NewGuid(),
                            Name = itemName,
                            Description = faker.Random.Bool(0.6f) ? faker.Lorem.Sentence() : null,
                            IsComplete = faker.Random.Bool(0.4f) ? 1 : 0,
                            ItemGroupId = groupId,
                        },
                        transaction: tx,
                        cancellationToken: ct
                    )
                );
            }
        }

        tx.Commit();
    }
}

using System.Data;
using Bogus;
using Dapper;

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
        app.MapPost(
                "/api/dev/seed",
                async (IDbConnection db, HttpContext context) =>
                {
                    Guid? userId = context.User.GetUserId();
                    if (userId is null)
                    {
                        return Results.Unauthorized();
                    }

                    var faker = new Faker();

                    // Clear existing data
                    await db.ExecuteAsync("DELETE FROM Items");
                    await db.ExecuteAsync("DELETE FROM Members");
                    await db.ExecuteAsync("DELETE FROM ItemGroups");

                    using var transaction = db.BeginTransaction();

                    for (int g = 0; g < 10; g++)
                    {
                        Guid groupId = Guid.NewGuid();
                        string groupName =
                            faker.PickRandom(contexts) + " " + faker.PickRandom(types);

                        await db.ExecuteAsync(
                            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
                            new { Id = groupId, Name = groupName },
                            transaction
                        );

                        await db.ExecuteAsync(
                            "INSERT INTO Members (ItemGroupId, MemberId) VALUES (@ItemGroupId, @MemberId)",
                            new { ItemGroupId = groupId, MemberId = userId.Value },
                            transaction
                        );

                        int itemCount = Random.Shared.Next(20, 31);
                        for (int i = 0; i < itemCount; i++)
                        {
                            string verb = faker.PickRandom(verbs);
                            string obj = faker.PickRandom(objects);
                            string itemName = faker.Random.Bool(0.4f)
                                ? $"{verb} {obj} {faker.PickRandom(qualifiers)}"
                                : $"{verb} {obj}";

                            await db.ExecuteAsync(
                                "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
                                new
                                {
                                    Id = Guid.NewGuid(),
                                    Name = itemName,
                                    Description = faker.Random.Bool(0.6f)
                                        ? faker.Lorem.Sentence()
                                        : (string?)null,
                                    IsComplete = faker.Random.Bool(0.4f) ? 1 : 0,
                                    ItemGroupId = groupId,
                                },
                                transaction
                            );
                        }
                    }

                    transaction.Commit();
                    return Results.NoContent();
                }
            )
            .RequireAuthorization()
            .WithTags("Dev");
    }
}

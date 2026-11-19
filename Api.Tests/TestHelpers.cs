using System.Security.Claims;
using Api.Checklist;
using Checklist.Domain;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

internal static class TestHelpers
{
    public static ClaimsPrincipal CreatePrincipal(Guid userId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    public static HttpRequest CreateHttpRequest()
    {
        return new DefaultHttpContext().Request;
    }

    public static CapturingNotifier CreateNotifier()
    {
        return new CapturingNotifier();
    }

    /// <summary>Inserts a list called "Group" with the given members (none by default).</summary>
    public static async Task InsertItemGroup(
        this SqlConnection db,
        Guid itemGroupId,
        params Guid[] memberIds
    )
    {
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        foreach (Guid memberId in memberIds)
        {
            await db.ExecuteAsync(
                "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
                new { MemberId = memberId, ItemGroupId = itemGroupId }
            );
        }
    }

    /// <summary>A <see cref="Name"/> for arranging tests; <paramref name="value"/> must not be blank.</summary>
    public static Name ValidName(string value) =>
        Name.TryCreate(value, out Name? name)
            ? name
            : throw new ArgumentException("Names can't be blank.", nameof(value));

    /// <summary>Inserts an item; it's complete when it has a <paramref name="completedAt"/>.</summary>
    public static async Task InsertItem(
        this SqlConnection db,
        Guid itemGroupId,
        Guid itemId,
        string name = "Item",
        DateTimeOffset? completedAt = null
    )
    {
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, IsComplete, CompletedAt, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @CompletedAt, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = name,
                IsComplete = completedAt is not null,
                CompletedAt = completedAt,
                ItemGroupId = itemGroupId,
            }
        );
    }

    public static Task<Item?> LoadItem(this SqlConnection db, Guid itemId) =>
        db.QuerySingleOrDefaultAsync<Item>(
            "SELECT Id, Name, Description, IsComplete, CompletedAt, ItemGroupId FROM Items WHERE Id = @Id",
            new { Id = itemId }
        );

    public static async Task<IReadOnlyList<Guid>> LoadMembers(
        this SqlConnection db,
        Guid itemGroupId
    ) =>
        [
            .. await db.QueryAsync<Guid>(
                "SELECT MemberId FROM Members WHERE ItemGroupId = @ItemGroupId",
                new { ItemGroupId = itemGroupId }
            ),
        ];

    /// <summary>
    /// The cleanup service deletes due lists on its own timer, which tests can't control.
    /// Tests call <see cref="AutoDeleteItemGroups"/> directly with a fixed time instead.
    /// </summary>
    public static void RemoveChecklistCleanupService(this IServiceCollection services)
    {
        services.Remove(
            services.Single(d => d.ImplementationType == typeof(ChecklistCleanupService))
        );
    }
}

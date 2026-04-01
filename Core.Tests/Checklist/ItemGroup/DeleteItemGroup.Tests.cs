using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class DeleteItemGroupTests
{
    [Fact]
    public async Task Execute_DeletesItemGroup_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "ToDelete" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );

        // Act
        var result = await DeleteItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm DB deletion
        var deleted = await db.QueryFirstOrDefaultAsync<ItemGroup>(
            "SELECT Id, Name FROM ItemGroups WHERE Id = @Id",
            new { Id = itemGroupId }
        );
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Execute_CascadeDeletesItemsAndMembers_WhenItemGroupIsDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = otherMemberId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "Item",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        // Act
        await DeleteItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert — cascade delete should remove items and members
        var remainingItems = await db.QueryAsync<Item>(
            "SELECT Id FROM Items WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId }
        );
        Assert.Empty(remainingItems);

        var remainingMembers = await db.QueryAsync<Guid>(
            "SELECT MemberId FROM Members WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId }
        );
        Assert.Empty(remainingMembers);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await DeleteItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "ToDelete" }
        );
        // No member added for this user

        // Act
        var result = await DeleteItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_NoError_WhenItemGroupDoesNotExist_ButUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        // Insert a placeholder group to satisfy the FK, add member, then remove the group
        var placeholderId = Guid.NewGuid();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Placeholder" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync(
            "DELETE FROM Members WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync("DELETE FROM ItemGroups WHERE Id = @Id", new { Id = itemGroupId });

        // Re-add only the member row, pointing to a group that no longer exists — not possible with FK.
        // Instead, seed a separate group to satisfy FK, then confirm IsMember still resolves correctly.
        // The scenario: member row exists for itemGroupId, but ItemGroup row does not.
        // SQLite enforces FK by default only when PRAGMA foreign_keys=ON. We can insert the orphan row.
        await db.ExecuteAsync("PRAGMA foreign_keys = OFF");
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync("PRAGMA foreign_keys = ON");

        // Act
        var result = await DeleteItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<NoContent>(result.Result);
    }
}

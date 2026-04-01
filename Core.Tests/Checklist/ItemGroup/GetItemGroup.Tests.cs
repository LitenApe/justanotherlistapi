using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Tests.Checklist.ItemGroupTests;

public class GetItemGroupTests
{
    [Fact]
    public async Task Execute_ReturnsOk_WhenUserIsMember_AndItemGroupExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "My Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        var ok = Assert.IsType<Ok<ItemGroup>>(result.Result);
        Assert.NotNull(ok.Value);
        Assert.Equal(itemGroupId, ok.Value.Id);
        Assert.Equal("My Group", ok.Value.Name);
    }

    [Fact]
    public async Task Execute_ReturnsAllItemsAndMembers_ForItemGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new
            {
                Id = item1Id,
                Name = "Item 1",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new
            {
                Id = item2Id,
                Name = "Item 2",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = member2Id, ItemGroupId = itemGroupId }
        );

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        var ok = Assert.IsType<Ok<ItemGroup>>(result.Result);
        var returnedGroup = ok.Value;
        Assert.NotNull(returnedGroup);
        Assert.Equal(2, returnedGroup.Items.Count);
        Assert.Contains(
            returnedGroup.Items,
            i => string.Equals(i.Name, "Item 1", StringComparison.Ordinal)
        );
        Assert.Contains(
            returnedGroup.Items,
            i => string.Equals(i.Name, "Item 2", StringComparison.Ordinal)
        );
        Assert.Equal(2, returnedGroup.Members.Count);
        Assert.Contains(returnedGroup.Members, m => m == userId);
        Assert.Contains(returnedGroup.Members, m => m == member2Id);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

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
            new { Id = itemGroupId, Name = "My Group" }
        );
        // No member for this user

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsNotFound_WhenItemGroupDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        // Insert member pointing to non-existent group (disable FK for this edge case)
        await db.ExecuteAsync("PRAGMA foreign_keys = OFF");
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync("PRAGMA foreign_keys = ON");

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<NotFound>(result.Result);
    }
}

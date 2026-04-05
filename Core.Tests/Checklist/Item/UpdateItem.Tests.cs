using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;

namespace Core.Tests.Checklist.ItemTests;

public sealed class UpdateItemTests
{
    [Fact]
    public async Task Execute_UpdatesItem_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        string oldName = "Old Item";
        string newName = "Updated Item";
        string newDescription = "Updated Description";
        bool newIsComplete = true;
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = oldName,
                Description = "Old Description",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        var request = new UpdateItem.Request
        {
            Name = newName,
            Description = newDescription,
            IsComplete = newIsComplete,
        };

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, db);

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm DB update
        Item? updated = await db.QueryFirstOrDefaultAsync<Item>(
            "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
            new { Id = itemId, ItemGroupId = itemGroupId }
        );
        Assert.NotNull(updated);
        Assert.Equal(newName, updated.Name);
        Assert.Equal(newDescription, updated.Description);
        Assert.Equal(newIsComplete, updated.IsComplete);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmptyOrWhitespace(string name)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "Old",
                Description = "Old",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        var request = new UpdateItem.Request
        {
            Name = name,
            Description = "Desc",
            IsComplete = false,
        };

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, db);

        // Assert
        Assert.IsType<BadRequest>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new UpdateItem.Request
        {
            Name = "Name",
            Description = "Desc",
            IsComplete = false,
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "Old",
                Description = "Old",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, db);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new UpdateItem.Request
        {
            Name = "Name",
            Description = "Desc",
            IsComplete = false,
        };
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "Old",
                Description = "Old",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, db);

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_NoError_WhenItemDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new UpdateItem.Request
        {
            Name = "Name",
            Description = "Desc",
            IsComplete = false,
        };
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, db);

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm item still does not exist
        Item? updated = await db.QueryFirstOrDefaultAsync<Item>(
            "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
            new { Id = itemId, ItemGroupId = itemGroupId }
        );
        Assert.Null(updated);
    }
}

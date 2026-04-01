using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Tests.Checklist.ItemTests;

public class CreateItemTests
{
    [Fact]
    public async Task Execute_CreatesItem_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = "Test Item",
            Description = "Test Description",
            IsComplete = false,
        };
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

        // Act
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, db, default);

        // Assert
        var created = Assert.IsType<Created<Item>>(result.Result);
        var item = created.Value;
        Assert.NotNull(item);
        Assert.Equal(request.Name, item.Name);
        Assert.Equal(request.Description, item.Description);
        Assert.Equal(request.IsComplete, item.IsComplete);
        Assert.Equal(itemGroupId, item.ItemGroupId);

        // Confirm DB write
        var dbItem = await db.QueryFirstOrDefaultAsync<Item>(
            "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE Id = @Id",
            new { item.Id }
        );
        Assert.NotNull(dbItem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmptyOrWhitespace(string name)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = name,
            Description = "Test Description",
            IsComplete = false,
        };
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

        // Act
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<BadRequest>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = "Test Item",
            Description = "Test Description",
            IsComplete = false,
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = "Test Item",
            Description = "Test Description",
            IsComplete = false,
        };
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        // No member for this user

        // Act
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }
}

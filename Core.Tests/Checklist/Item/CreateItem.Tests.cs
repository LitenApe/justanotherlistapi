using System.Security.Claims;
using Core.AuditLog;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;

namespace Core.Tests.Checklist.ItemTests;

public sealed class CreateItemTests
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
        Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await CreateItem.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                new AuditContext(),
                default
            );

        // Assert
        Created<Item> created = Assert.IsType<Created<Item>>(result.Result);
        Item? item = created.Value;
        Assert.NotNull(item);
        Assert.Equal(request.Name, item.Name);
        Assert.Equal(request.Description, item.Description);
        Assert.Equal(request.IsComplete, item.IsComplete);
        Assert.Equal(itemGroupId, item.ItemGroupId);

        // Confirm DB write
        Item? dbItem = await db.QueryFirstOrDefaultAsync<Item>(
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
        Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await CreateItem.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                new AuditContext(),
                default
            );

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

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await CreateItem.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                new AuditContext(),
                default
            );

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
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        // No member for this user

        // Act
        Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await CreateItem.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                new AuditContext(),
                default
            );

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }
}

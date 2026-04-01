using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class UpdateItemGroupTests
{
    [Fact]
    public async Task Execute_UpdatesItemGroup_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        string oldName = "Old Name";
        string newName = "New Name";
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = oldName }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );

        var request = new UpdateItemGroup.Request { Name = newName };

        // Act
        var result = await UpdateItemGroup.Execute(
            itemGroupId,
            request,
            claimsPrincipal,
            db,
            default
        );

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm DB update
        var updated = await db.QueryFirstOrDefaultAsync<ItemGroup>(
            "SELECT Id, Name FROM ItemGroups WHERE Id = @Id",
            new { Id = itemGroupId }
        );
        Assert.NotNull(updated);
        Assert.Equal(newName, updated.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmptyOrWhitespace(string name)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();

        var request = new UpdateItemGroup.Request { Name = name };

        // Act
        var result = await UpdateItemGroup.Execute(
            itemGroupId,
            request,
            claimsPrincipal,
            db,
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
        var request = new UpdateItemGroup.Request { Name = "Any Name" };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await UpdateItemGroup.Execute(
            itemGroupId,
            request,
            claimsPrincipal,
            db,
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
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        // Add ItemGroup but not Member
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Old Name" }
        );

        var request = new UpdateItemGroup.Request { Name = "New Name" };

        // Act
        var result = await UpdateItemGroup.Execute(
            itemGroupId,
            request,
            claimsPrincipal,
            db,
            default
        );

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }
}

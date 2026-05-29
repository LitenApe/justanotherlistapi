using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;

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
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);
        var notifier = new CapturingNotifier();

        await using SqliteConnection db = await TestDatabase.CreateAsync();
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
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItemGroup.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                notifier,
                TestHelpers.CreateHttpRequest()
            );

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm DB update
        ItemGroup? updated = await db.QueryFirstOrDefaultAsync<ItemGroup>(
            "SELECT Id, Name FROM ItemGroups WHERE Id = @Id",
            new { Id = itemGroupId }
        );
        Assert.NotNull(updated);
        Assert.Equal(newName, updated.Name);

        // Confirm notification
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupRenamedNotification groupRenamed =
            Assert.IsType<CapturingNotifier.GroupRenamedNotification>(notification);
        Assert.Equal(itemGroupId, groupRenamed.GroupId);
        Assert.Equal(newName, groupRenamed.Name);
        Assert.Null(groupRenamed.ExcludeConnectionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmptyOrWhitespace(string name)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        var request = new UpdateItemGroup.Request { Name = name };

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItemGroup.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest()
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

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItemGroup.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest()
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
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        // Add ItemGroup but not Member
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Old Name" }
        );

        var request = new UpdateItemGroup.Request { Name = "New Name" };

        // Act
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItemGroup.Execute(
                itemGroupId,
                request,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest()
            );

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_PassesSignalRConnectionId_WhenHeaderIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);
        var notifier = new CapturingNotifier();

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Old" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );

        var request = new UpdateItemGroup.Request { Name = "New" };

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest();
        httpRequest.Headers["X-SignalR-Connection-Id"] = "conn-ug-001";

        // Act
        await UpdateItemGroup.Execute(
            itemGroupId,
            request,
            claimsPrincipal,
            db,
            notifier,
            httpRequest
        );

        // Assert
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupRenamedNotification groupRenamed =
            Assert.IsType<CapturingNotifier.GroupRenamedNotification>(notification);
        Assert.Equal("conn-ug-001", groupRenamed.ExcludeConnectionId);
    }
}

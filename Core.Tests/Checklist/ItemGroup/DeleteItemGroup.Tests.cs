using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http;
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
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);
        var notifier = new CapturingNotifier();

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
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> result =
            await DeleteItemGroup.Execute(
                itemGroupId,
                claimsPrincipal,
                db,
                notifier,
                TestHelpers.CreateHttpRequest(),
                default
            );

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm DB deletion
        ItemGroup? deleted = await db.QueryFirstOrDefaultAsync<ItemGroup>(
            "SELECT Id, Name FROM ItemGroups WHERE Id = @Id",
            new { Id = itemGroupId }
        );
        Assert.Null(deleted);

        // Confirm notification
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupDeletedNotification groupDeleted =
            Assert.IsType<CapturingNotifier.GroupDeletedNotification>(notification);
        Assert.Equal(itemGroupId, groupDeleted.GroupId);
        Assert.Null(groupDeleted.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_CascadeDeletesItemsAndMembers_WhenItemGroupIsDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

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
        await DeleteItemGroup.Execute(
            itemGroupId,
            claimsPrincipal,
            db,
            new CapturingNotifier(),
            TestHelpers.CreateHttpRequest(),
            default
        );

        // Assert — cascade delete should remove items and members
        IEnumerable<Item> remainingItems = await db.QueryAsync<Item>(
            "SELECT Id FROM Items WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId }
        );
        Assert.Empty(remainingItems);

        IEnumerable<Guid> remainingMembers = await db.QueryAsync<Guid>(
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
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> result =
            await DeleteItemGroup.Execute(
                itemGroupId,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
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
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "ToDelete" }
        );
        // No member added for this user

        // Act
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> result =
            await DeleteItemGroup.Execute(
                itemGroupId,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                default
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

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "ToDelete" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest();
        httpRequest.Headers["X-SignalR-Connection-Id"] = "conn-dg-002";

        // Act
        await DeleteItemGroup.Execute(
            itemGroupId,
            claimsPrincipal,
            db,
            notifier,
            httpRequest,
            default
        );

        // Assert
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupDeletedNotification groupDeleted =
            Assert.IsType<CapturingNotifier.GroupDeletedNotification>(notification);
        Assert.Equal("conn-dg-002", groupDeleted.ExcludeConnectionId);
    }
}

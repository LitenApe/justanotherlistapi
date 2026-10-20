using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

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
        var notifier = new CapturingNotifier();

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
            await UpdateItem.Execute(
                itemGroupId,
                itemId,
                request,
                claimsPrincipal,
                db,
                notifier,
                TestHelpers.CreateHttpRequest(),
                TimeProvider.System,
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm DB update
        Item? updated = await db.QueryFirstOrDefaultAsync<Item>(
            "SELECT Id, Name, Description, IsComplete, CompletedAt, ItemGroupId FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
            new { Id = itemId, ItemGroupId = itemGroupId }
        );
        Assert.NotNull(updated);
        Assert.Equal(newName, updated.Name);
        Assert.Equal(newDescription, updated.Description);
        Assert.Equal(newIsComplete, updated.IsComplete);
        Assert.NotNull(updated.CompletedAt);

        // Confirm notification
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.ItemUpdatedNotification itemUpdated =
            Assert.IsType<CapturingNotifier.ItemUpdatedNotification>(notification);
        Assert.Equal(itemGroupId, itemUpdated.GroupId);
        Assert.Equal(itemId, itemUpdated.Item.Id);
        Assert.Equal(newName, itemUpdated.Item.Name);
        Assert.Equal(updated.CompletedAt, itemUpdated.Item.CompletedAt);
        Assert.Null(itemUpdated.ExcludeConnectionId);
    }

    [Fact]
    public async Task UpdateData_TracksCompletionTime_AcrossCompleteEditAndReopen()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var completedAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, 0, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "Milk",
                ItemGroupId = itemGroupId,
            }
        );

        // Act & Assert: completing sets the time.
        DateTimeOffset? afterComplete = await UpdateItem.UpdateData(
            itemGroupId,
            itemId,
            new UpdateItem.Request { Name = "Milk", IsComplete = true },
            completedAt,
            db,
            ct
        );
        Assert.Equal(completedAt, afterComplete);

        // Saving a completed item again keeps the original time.
        DateTimeOffset? afterEdit = await UpdateItem.UpdateData(
            itemGroupId,
            itemId,
            new UpdateItem.Request { Name = "Oat milk", IsComplete = true },
            completedAt.AddDays(2),
            db,
            ct
        );
        Assert.Equal(completedAt, afterEdit);

        // Reopening clears it.
        DateTimeOffset? afterReopen = await UpdateItem.UpdateData(
            itemGroupId,
            itemId,
            new UpdateItem.Request { Name = "Oat milk", IsComplete = false },
            completedAt.AddDays(3),
            db,
            ct
        );
        Assert.Null(afterReopen);
        Assert.Null(
            await db.ExecuteScalarAsync<DateTimeOffset?>(
                "SELECT CompletedAt FROM Items WHERE Id = @Id",
                new { Id = itemId }
            )
        );
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
            await UpdateItem.Execute(
                itemGroupId,
                itemId,
                request,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                TimeProvider.System,
                TestContext.Current.CancellationToken
            );

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

        await using var db = await TestDatabase.CreateAsync();
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
            await UpdateItem.Execute(
                itemGroupId,
                itemId,
                request,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                TimeProvider.System,
                TestContext.Current.CancellationToken
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
        var itemId = Guid.NewGuid();
        var request = new UpdateItem.Request
        {
            Name = "Name",
            Description = "Desc",
            IsComplete = false,
        };
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
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
            await UpdateItem.Execute(
                itemGroupId,
                itemId,
                request,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                TimeProvider.System,
                TestContext.Current.CancellationToken
            );

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
        Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> result =
            await UpdateItem.Execute(
                itemGroupId,
                itemId,
                request,
                claimsPrincipal,
                db,
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                TimeProvider.System,
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm item still does not exist
        Item? updated = await db.QueryFirstOrDefaultAsync<Item>(
            "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
            new { Id = itemId, ItemGroupId = itemGroupId }
        );
        Assert.Null(updated);
    }

    [Fact]
    public async Task Execute_PassesSignalRConnectionId_WhenHeaderIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);
        var notifier = new CapturingNotifier();

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
            Name = "New",
            Description = "New",
            IsComplete = true,
        };

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest();
        httpRequest.Headers["X-SignalR-Connection-Id"] = "conn-xyz-789";

        // Act
        await UpdateItem.Execute(
            itemGroupId,
            itemId,
            request,
            claimsPrincipal,
            db,
            notifier,
            httpRequest,
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        // Assert
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.ItemUpdatedNotification itemUpdated =
            Assert.IsType<CapturingNotifier.ItemUpdatedNotification>(notification);
        Assert.Equal("conn-xyz-789", itemUpdated.ExcludeConnectionId);
    }
}

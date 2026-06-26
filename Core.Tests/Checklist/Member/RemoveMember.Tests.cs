using System.Security.Claims;
using Core.AuditLog;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Tests.Checklist.MemberTests;

public sealed class RemoveMemberTests
{
    [Fact]
    public async Task Execute_RemovesMember_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();
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
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = memberIdToRemove, ItemGroupId = itemGroupId }
        );

        // Act
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await RemoveMember.Execute(
                itemGroupId,
                memberIdToRemove,
                claimsPrincipal,
                db,
                new AuditContext(),
                notifier,
                TestHelpers.CreateHttpRequest(),
                default
            );

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm member is removed
        Guid? removed = await db.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT MemberId FROM Members WHERE ItemGroupId = @ItemGroupId AND MemberId = @MemberId",
            new { ItemGroupId = itemGroupId, MemberId = memberIdToRemove }
        );
        Assert.Null(removed);

        // Confirm notification
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.MemberRemovedNotification memberRemoved =
            Assert.IsType<CapturingNotifier.MemberRemovedNotification>(notification);
        Assert.Equal(itemGroupId, memberRemoved.GroupId);
        Assert.Equal(memberIdToRemove, memberRemoved.MemberId);
        Assert.Null(memberRemoved.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();

        // Act
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await RemoveMember.Execute(
                itemGroupId,
                memberIdToRemove,
                claimsPrincipal,
                db,
                new AuditContext(),
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
        var memberIdToRemove = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        // User is not a member

        // Act
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await RemoveMember.Execute(
                itemGroupId,
                memberIdToRemove,
                claimsPrincipal,
                db,
                new AuditContext(),
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                default
            );

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsConflict_WhenRemovingLastMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
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
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await RemoveMember.Execute(
                itemGroupId,
                userId,
                claimsPrincipal,
                db,
                new AuditContext(),
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                default
            );

        // Assert
        Assert.IsType<Conflict>(result.Result);
    }

    [Fact]
    public async Task Execute_NoError_WhenMemberDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();
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
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await RemoveMember.Execute(
                itemGroupId,
                memberIdToRemove,
                claimsPrincipal,
                db,
                new AuditContext(),
                new CapturingNotifier(),
                TestHelpers.CreateHttpRequest(),
                default
            );

        // Assert
        Assert.IsType<NoContent>(result.Result);
    }

    [Fact]
    public async Task Execute_PassesSignalRConnectionId_WhenHeaderIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();
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
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = memberIdToRemove, ItemGroupId = itemGroupId }
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest();
        httpRequest.Headers["X-SignalR-Connection-Id"] = "conn-rm-004";

        // Act
        await RemoveMember.Execute(
            itemGroupId,
            memberIdToRemove,
            claimsPrincipal,
            db,
            new AuditContext(),
            notifier,
            httpRequest,
            default
        );

        // Assert
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.MemberRemovedNotification memberRemoved =
            Assert.IsType<CapturingNotifier.MemberRemovedNotification>(notification);
        Assert.Equal("conn-rm-004", memberRemoved.ExcludeConnectionId);
    }
}

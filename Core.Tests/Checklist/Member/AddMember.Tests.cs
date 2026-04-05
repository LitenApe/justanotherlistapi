using System.Security.Claims;
using Core.AuditLog;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;

namespace Core.Tests.Checklist.MemberTests;

public sealed class AddMemberTests
{
    [Fact]
    public async Task Execute_AddsMember_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
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
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await AddMember.Execute(
                itemGroupId,
                newMemberId,
                claimsPrincipal,
                db,
                new AuditContext()
            );

        // Assert
        Assert.IsType<NoContent>(result.Result);

        // Confirm DB write
        Guid? added = await db.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT MemberId FROM Members WHERE ItemGroupId = @ItemGroupId AND MemberId = @MemberId",
            new { ItemGroupId = itemGroupId, MemberId = newMemberId }
        );
        Assert.NotNull(added);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await AddMember.Execute(
                itemGroupId,
                newMemberId,
                claimsPrincipal,
                db,
                new AuditContext()
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
        var newMemberId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        // User is not a member

        // Act
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await AddMember.Execute(
                itemGroupId,
                newMemberId,
                claimsPrincipal,
                db,
                new AuditContext()
            );

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsConflict_WhenMemberAlreadyExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
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
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = existingMemberId, ItemGroupId = itemGroupId }
        );

        // Act
        Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult, Conflict> result =
            await AddMember.Execute(
                itemGroupId,
                existingMemberId,
                claimsPrincipal,
                db,
                new AuditContext()
            );

        // Assert
        Assert.IsType<Conflict>(result.Result);
    }
}

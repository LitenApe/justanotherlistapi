using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Core.Tests.Checklist.MemberTests;

public sealed class GetMembersTests
{
    [Fact]
    public async Task Execute_ReturnsAllMemberIds_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
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
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = otherUserId, ItemGroupId = itemGroupId }
        );

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        var ok = Assert.IsType<Ok<List<Guid>>>(result.Result);
        var members = ok.Value;
        Assert.NotNull(members);
        Assert.Equal(2, members.Count);
        Assert.Contains(userId, members);
        Assert.Contains(otherUserId, members);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, db, default);

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
            new { Id = itemGroupId, Name = "Group" }
        );
        // No member for this user

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserMembershipIsRevoked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
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
        // Remove all members to simulate revoked membership
        await db.ExecuteAsync(
            "DELETE FROM Members WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId }
        );

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
    }
}

using System.Security.Claims;
using Core.Checklist;
using Dapper;

namespace Core.Tests.Checklist;

public sealed class ChecklistAuthorizationExtensionsTests
{
    [Fact]
    public async Task ExecuteAsAuthenticatedUser_InvokesAuthorizedPath_WhenUserIdExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        ClaimsPrincipal principal = TestHelpers.CreatePrincipal(userId);

        // Act
        Guid result = await principal.ExecuteAsAuthenticatedUser(
            onAuthorized: resolvedUserId => Task.FromResult(resolvedUserId),
            onUnauthorized: Guid.Empty
        );

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task ExecuteAsAuthenticatedUser_InvokesUnauthorizedPath_WhenUserIdMissing()
    {
        // Arrange
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity()
        );

        // Act
        string result = await principal.ExecuteAsAuthenticatedUser(
            onAuthorized: _ => Task.FromResult("authorized"),
            onUnauthorized: "unauthorized"
        );

        // Assert
        Assert.Equal("unauthorized", result);
    }

    [Fact]
    public async Task ExecuteAsItemGroupMember_InvokesAuthorizedPath_WhenUserIsMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        ClaimsPrincipal principal = TestHelpers.CreatePrincipal(userId);

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
        Guid result = await db.ExecuteAsItemGroupMember(
            itemGroupId,
            principal,
            onAuthorized: resolvedUserId => Task.FromResult(resolvedUserId),
            onUnauthorized: Guid.Empty,
            onForbidden: Guid.Parse("11111111-1111-1111-1111-111111111111")
        );

        // Assert
        Assert.Equal(userId, result);
    }

    [Fact]
    public async Task ExecuteAsItemGroupMember_InvokesUnauthorizedPath_WhenUserIdMissing()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity()
        );

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        string result = await db.ExecuteAsItemGroupMember(
            itemGroupId,
            principal,
            onAuthorized: _ => Task.FromResult("authorized"),
            onUnauthorized: "unauthorized",
            onForbidden: "forbidden"
        );

        // Assert
        Assert.Equal("unauthorized", result);
    }

    [Fact]
    public async Task ExecuteAsItemGroupMember_InvokesForbiddenPath_WhenUserIsNotMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        ClaimsPrincipal principal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        string result = await db.ExecuteAsItemGroupMember(
            itemGroupId,
            principal,
            onAuthorized: _ => Task.FromResult("authorized"),
            onUnauthorized: "unauthorized",
            onForbidden: "forbidden"
        );

        // Assert
        Assert.Equal("forbidden", result);
    }

    [Fact]
    public async Task ExecuteAsItemGroupMember_DoesNotInvokeAuthorizedPath_WhenUnauthorizedOrForbidden()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var missingUserPrincipal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity()
        );
        ClaimsPrincipal nonMemberPrincipal = TestHelpers.CreatePrincipal(Guid.NewGuid());
        int authorizedCalls = 0;

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        _ = await db.ExecuteAsItemGroupMember(
            itemGroupId,
            missingUserPrincipal,
            onAuthorized: _ =>
            {
                authorizedCalls++;
                return Task.FromResult(0);
            },
            onUnauthorized: -1,
            onForbidden: -2
        );

        _ = await db.ExecuteAsItemGroupMember(
            itemGroupId,
            nonMemberPrincipal,
            onAuthorized: _ =>
            {
                authorizedCalls++;
                return Task.FromResult(0);
            },
            onUnauthorized: -1,
            onForbidden: -2
        );

        // Assert
        Assert.Equal(0, authorizedCalls);
    }
}

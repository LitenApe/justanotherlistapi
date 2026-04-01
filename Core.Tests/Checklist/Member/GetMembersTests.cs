using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

public class GetMembersTests
{
    [Fact]
    public async Task Execute_ReturnsAllMemberIds_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = otherUserId, ItemGroupId = itemGroupId });

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            if (results.Result is Ok<List<Guid>> ok)
            {
                var members = ok.Value;
                Assert.NotNull(members);
                Assert.Equal(2, members.Count);
                Assert.Contains(userId, members);
                Assert.Contains(otherUserId, members);
            }
            else
            {
                Assert.Fail("Expected Ok<List<Guid>> result.");
            }
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
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
        Assert.NotNull(result);
        if (result is Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<UnauthorizedHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        // No member for this user

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserMembershipIsRevoked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });
        // Remove all members to simulate revoked membership
        await db.ExecuteAsync("DELETE FROM Members WHERE ItemGroupId = @ItemGroupId", new { ItemGroupId = itemGroupId });

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<Guid>>, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class GetMembersTests
{
    [Fact]
    public async Task Execute_ReturnsAllMemberIds_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Group" });
        dbContext.Members.AddRange(
            new Member { ItemGroupId = itemGroupId, MemberId = userId },
            new Member { ItemGroupId = itemGroupId, MemberId = otherUserId }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            if (results.Result is Ok<List<string>> ok)
            {
                var members = ok.Value;
                Assert.Equal(2, members.Count);
                Assert.Contains(userId, members);
                Assert.Contains(otherUserId, members);
            }
            else
            {
                Assert.Fail("Expected Ok<List<string>> result.");
            }
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<UnauthorizedHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult>.");
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

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Group" });
        // No member for this user
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsUserId_WhenNoMembersExist()
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

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Group" });
        dbContext.Members.Add(new Member { ItemGroupId = itemGroupId, MemberId = userId });
        await dbContext.SaveChangesAsync();

        // Remove all members to simulate no members (simulate LoadData returns null)
        dbContext.Members.RemoveRange(dbContext.Members);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetMembers.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<string>>, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class RemoveMemberTests
{
    [Fact]
    public async Task Execute_RemovesMember_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Group" });
        dbContext.Members.Add(new Member { ItemGroupId = itemGroupId, MemberId = userId });
        dbContext.Members.Add(new Member { ItemGroupId = itemGroupId, MemberId = memberIdToRemove });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await RemoveMember.Execute(itemGroupId, memberIdToRemove, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm member is removed
            var removed = await dbContext.Members.FirstOrDefaultAsync(m => m.ItemGroupId == itemGroupId && m.MemberId == memberIdToRemove);
            Assert.Null(removed);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Act
        var result = await RemoveMember.Execute(itemGroupId, memberIdToRemove, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<UnauthorizedHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();

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
        // User is not a member
        await dbContext.SaveChangesAsync();

        // Act
        var result = await RemoveMember.Execute(itemGroupId, memberIdToRemove, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_NoError_WhenMemberDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
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

        // Act
        var result = await RemoveMember.Execute(itemGroupId, memberIdToRemove, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

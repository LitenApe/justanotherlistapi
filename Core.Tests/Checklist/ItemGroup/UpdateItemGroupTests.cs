using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class UpdateItemGroupTests
{
    [Fact]
    public async Task Execute_UpdatesItemGroup_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var oldName = "Old Name";
        var newName = "New Name";

        // ClaimsPrincipal with userId
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        // In-memory DB with existing ItemGroup and Member
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);
        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = oldName });
        dbContext.Members.Add(new Member { ItemGroupId = itemGroupId, MemberId = userId });
        await dbContext.SaveChangesAsync();

        var request = new UpdateItemGroup.Request { Name = newName };

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm DB update
            var updated = await dbContext.ItemGroups.FirstOrDefaultAsync(ig => ig.Id == itemGroupId);
            Assert.NotNull(updated);
            Assert.Equal(newName, updated.Name);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmpty()
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

        var request = new UpdateItemGroup.Request { Name = "" };

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<BadRequest>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var request = new UpdateItemGroup.Request { Name = "Any Name" };

        // ClaimsPrincipal with no userId
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<UnauthorizedHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
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

        // Add ItemGroup but not Member
        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Old Name" });
        await dbContext.SaveChangesAsync();

        var request = new UpdateItemGroup.Request { Name = "New Name" };

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class DeleteItemTests
{
    [Fact]
    public async Task Execute_DeletesItem_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

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
        dbContext.Items.Add(new Item { Id = itemId, Name = "Item", Description = "Desc", IsComplete = false, ItemGroupId = itemGroupId });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm item is deleted
            var deleted = await dbContext.Items.FirstOrDefaultAsync(i => i.Id == itemId && i.ItemGroupId == itemGroupId);
            Assert.Null(deleted);
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
        var itemId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, dbContext, default);

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
        var itemId = Guid.NewGuid();

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
        dbContext.Items.Add(new Item { Id = itemId, Name = "Item", Description = "Desc", IsComplete = false, ItemGroupId = itemGroupId });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, dbContext, default);

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
    public async Task Execute_NoError_WhenItemDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

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

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm item still does not exist
            var deleted = await dbContext.Items.FirstOrDefaultAsync(i => i.Id == itemId && i.ItemGroupId == itemGroupId);
            Assert.Null(deleted);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

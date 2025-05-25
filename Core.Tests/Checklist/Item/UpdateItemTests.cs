using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class UpdateItemTests
{
    [Fact]
    public async Task Execute_UpdatesItem_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var oldName = "Old Item";
        var newName = "Updated Item";
        var newDescription = "Updated Description";
        var newIsComplete = true;

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
        dbContext.Items.Add(new Item { Id = itemId, Name = oldName, Description = "Old Description", IsComplete = false, ItemGroupId = itemGroupId });
        await dbContext.SaveChangesAsync();

        var request = new UpdateItem.Request
        {
            Name = newName,
            Description = newDescription,
            IsComplete = newIsComplete
        };

        // Act
        var result = await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm DB update
            var updated = await dbContext.Items.FirstOrDefaultAsync(i => i.Id == itemId && i.ItemGroupId == itemGroupId);
            Assert.NotNull(updated);
            Assert.Equal(newName, updated.Name);
            Assert.Equal(newDescription, updated.Description);
            Assert.Equal(newIsComplete, updated.IsComplete);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmptyOrWhitespace(string name)
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
        dbContext.Items.Add(new Item { Id = itemId, Name = "Old", Description = "Old", IsComplete = false, ItemGroupId = itemGroupId });
        await dbContext.SaveChangesAsync();

        var request = new UpdateItem.Request
        {
            Name = name,
            Description = "Desc",
            IsComplete = false
        };

        // Act
        var result = await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, dbContext, default);

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
        var itemId = Guid.NewGuid();
        var request = new UpdateItem.Request
        {
            Name = "Name",
            Description = "Desc",
            IsComplete = false
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Group" });
        dbContext.Items.Add(new Item { Id = itemId, Name = "Old", Description = "Old", IsComplete = false, ItemGroupId = itemGroupId });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, dbContext, default);

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
        var itemId = Guid.NewGuid();
        var request = new UpdateItem.Request
        {
            Name = "Name",
            Description = "Desc",
            IsComplete = false
        };

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
        dbContext.Items.Add(new Item { Id = itemId, Name = "Old", Description = "Old", IsComplete = false, ItemGroupId = itemGroupId });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, dbContext, default);

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

    [Fact]
    public async Task Execute_NoError_WhenItemDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new UpdateItem.Request
        {
            Name = "Name",
            Description = "Desc",
            IsComplete = false
        };

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
        var result = await UpdateItem.Execute(itemGroupId, itemId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm item still does not exist
            var updated = await dbContext.Items.FirstOrDefaultAsync(i => i.Id == itemId && i.ItemGroupId == itemGroupId);
            Assert.Null(updated);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

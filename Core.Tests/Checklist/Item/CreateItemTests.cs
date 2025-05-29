using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class CreateItemTests
{
    [Fact]
    public async Task Execute_CreatesItem_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = "Test Item",
            Description = "Test Description",
            IsComplete = false
        };

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

        // User is a member
        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Group" });
        dbContext.Members.Add(new Member { ItemGroupId = itemGroupId, MemberId = userId });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            if (results.Result is Created<Item> created)
            {
                var item = created.Value;
                Assert.NotNull(item);
                Assert.Equal(request.Name, item.Name);
                Assert.Equal(request.Description, item.Description);
                Assert.Equal(request.IsComplete, item.IsComplete);
                Assert.Equal(itemGroupId, item.ItemGroupId);

                // Confirm DB write
                var dbItem = await dbContext.Items.FirstOrDefaultAsync(i => i.Id == item.Id);
                Assert.NotNull(dbItem);
            }
            else
            {
                Assert.Fail("Expected Created<Item> result.");
            }
        }
        else
        {
            Assert.Fail("Expected Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmptyOrWhitespace(string name)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = name,
            Description = "Test Description",
            IsComplete = false
        };

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
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<BadRequest>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = "Test Item",
            Description = "Test Description",
            IsComplete = false
        };

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "Group" });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<UnauthorizedHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var itemGroupId = Guid.NewGuid();
        var request = new CreateItem.Request
        {
            Name = "Test Item",
            Description = "Test Description",
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
        await dbContext.SaveChangesAsync();

        // Act
        var result = await CreateItem.Execute(itemGroupId, request, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Created<Item>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

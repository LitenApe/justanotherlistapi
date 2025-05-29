using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class GetItemGroupTests
{
    [Fact]
    public async Task Execute_ReturnsOk_WhenUserIsMember_AndItemGroupExists()
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

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        var itemGroup = new ItemGroup { Id = itemGroupId, Name = "My Group" };
        dbContext.ItemGroups.Add(itemGroup);
        dbContext.Members.Add(new Member { ItemGroupId = itemGroupId, MemberId = userId });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            if (results.Result is Ok<ItemGroup> ok)
            {
                Assert.NotNull(ok.Value);
                Assert.Equal(itemGroupId, ok.Value.Id);
                Assert.Equal("My Group", ok.Value.Name);
            }
            else
            {
                Assert.Fail("Expected Ok<ItemGroup> result.");
            }
        }
        else
        {
            Assert.Fail("Expected Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsAllItemsAndMembers_ForItemGroup()
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

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Add item group, items, and members
        var itemGroup = new ItemGroup { Id = itemGroupId, Name = "Group" };
        var item1 = new Item { Id = Guid.NewGuid(), Name = "Item 1", ItemGroupId = itemGroupId };
        var item2 = new Item { Id = Guid.NewGuid(), Name = "Item 2", ItemGroupId = itemGroupId };
        var member1 = new Member { ItemGroupId = itemGroupId, MemberId = userId };
        var member2 = new Member { ItemGroupId = itemGroupId, MemberId = Guid.NewGuid() };

        dbContext.ItemGroups.Add(itemGroup);
        dbContext.Items.AddRange(item1, item2);
        dbContext.Members.AddRange(member1, member2);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            if (results.Result is Ok<ItemGroup> ok)
            {
                var returnedGroup = ok.Value;
                Assert.NotNull(returnedGroup);
                Assert.Equal(2, returnedGroup.Items.Count);
                Assert.Contains(returnedGroup.Items, i => i.Name == "Item 1");
                Assert.Contains(returnedGroup.Items, i => i.Name == "Item 2");
                Assert.Equal(2, returnedGroup.Members.Count);
                Assert.Contains(returnedGroup.Members, m => m.MemberId == userId);
                Assert.Contains(returnedGroup.Members, m => m.MemberId == member2.MemberId);
            }
            else
            {
                Assert.Fail("Expected Ok<ItemGroup> result.");
            }
        }
        else
        {
            Assert.Fail("Expected Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>.");
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
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<UnauthorizedHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>.");
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

        dbContext.ItemGroups.Add(new ItemGroup { Id = itemGroupId, Name = "My Group" });
        // No member for this user
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<ForbidHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsNotFound_WhenItemGroupDoesNotExist()
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

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Add member but not the item group itself
        dbContext.Members.Add(new Member { ItemGroupId = itemGroupId, MemberId = userId });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NotFound>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<ItemGroup>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class GetItemGroupsTests
{
    [Fact]
    public async Task Execute_ReturnsAllItemGroups_ForAuthenticatedUser()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var otherUserId = Guid.NewGuid().ToString();
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();

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

        // Add two groups, user is a member of both
        dbContext.ItemGroups.AddRange(
            new ItemGroup { Id = group1Id, Name = "Group 1" },
            new ItemGroup { Id = group2Id, Name = "Group 2" }
        );
        dbContext.Members.AddRange(
            new Member { ItemGroupId = group1Id, MemberId = userId },
            new Member { ItemGroupId = group2Id, MemberId = userId },
            new Member { ItemGroupId = group2Id, MemberId = otherUserId }
        );
        // Add items (one complete, one incomplete) to group1
        dbContext.Items.AddRange(
            new Item { Id = Guid.NewGuid(), Name = "Incomplete", IsComplete = false, ItemGroupId = group1Id },
            new Item { Id = Guid.NewGuid(), Name = "Complete", IsComplete = true, ItemGroupId = group1Id }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult> results)
        {
            if (results.Result is Ok<List<ItemGroup>> ok)
            {
                var groups = ok.Value;
                Assert.Equal(2, groups.Count);
                var group1 = groups.FirstOrDefault(g => g.Id == group1Id);
                Assert.NotNull(group1);
                // Only incomplete items should be included
                Assert.Single(group1.Items);
                Assert.Equal("Incomplete", group1.Items.First().Name);
            }
            else
            {
                Assert.Fail("Expected Ok<List<ItemGroup>> result.");
            }
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsEmptyList_WhenUserHasNoGroups()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
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

        // No groups or members for this user

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult> results)
        {
            if (results.Result is Ok<List<ItemGroup>> ok)
            {
                Assert.Empty(ok.Value);
            }
            else
            {
                Assert.Fail("Expected Ok<List<ItemGroup>> result.");
            }
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, dbContext, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult> results)
        {
            Assert.IsType<UnauthorizedHttpResult>(results.Result);
        }
        else
        {
            Assert.Fail("Expected Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult>.");
        }
    }
}

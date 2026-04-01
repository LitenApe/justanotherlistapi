using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

public class GetItemGroupsTests
{
    [Fact]
    public async Task Execute_ReturnsAllItemGroups_ForAuthenticatedUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = group1Id, Name = "Group 1" });
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = group2Id, Name = "Group 2" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = group1Id });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = group2Id });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = otherUserId, ItemGroupId = group2Id });
        // Add items (one complete, one incomplete) to group1
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new { Id = Guid.NewGuid(), Name = "Incomplete", IsComplete = false, ItemGroupId = group1Id });
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new { Id = Guid.NewGuid(), Name = "Complete", IsComplete = true, ItemGroupId = group1Id });

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult> results)
        {
            if (results.Result is Ok<List<ItemGroup>> ok)
            {
                var groups = ok.Value;
                Assert.NotNull(groups);
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

        await using var db = await TestDatabase.CreateAsync();
        // No groups or members for this user

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Ok<List<ItemGroup>>, UnauthorizedHttpResult> results)
        {
            if (results.Result is Ok<List<ItemGroup>> ok)
            {
                Assert.NotNull(ok.Value);
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

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, db, default);

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


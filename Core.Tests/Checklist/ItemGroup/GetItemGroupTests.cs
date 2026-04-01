using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

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

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "My Group" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

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
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new { Id = item1Id, Name = "Item 1", IsComplete = false, ItemGroupId = itemGroupId });
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new { Id = item2Id, Name = "Item 2", IsComplete = false, ItemGroupId = itemGroupId });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = member2Id, ItemGroupId = itemGroupId });

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

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
                Assert.Contains(returnedGroup.Members, m => m.MemberId == member2Id);
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

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

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

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "My Group" });
        // No member for this user

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

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

        await using var db = await TestDatabase.CreateAsync();
        // Insert member pointing to non-existent group (disable FK for this edge case)
        await db.ExecuteAsync("PRAGMA foreign_keys = OFF");
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });
        await db.ExecuteAsync("PRAGMA foreign_keys = ON");

        // Act
        var result = await GetItemGroup.Execute(itemGroupId, claimsPrincipal, db, default);

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

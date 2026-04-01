using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

public class DeleteItemTests
{
    [Fact]
    public async Task Execute_DeletesItem_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new { Id = itemId, Name = "Item", Description = "Desc", IsComplete = false, ItemGroupId = itemGroupId });

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm item is deleted
            var deleted = await db.QueryFirstOrDefaultAsync<Item>(
                "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
                new { Id = itemId, ItemGroupId = itemGroupId });
            Assert.Null(deleted);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }

    [Fact]
    public async Task Execute_DoesNotDeleteItem_WhenItemBelongsToAnotherGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = otherGroupId, Name = "Other Group" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new { Id = itemId, Name = "Item", Description = "Desc", IsComplete = false, ItemGroupId = otherGroupId });

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm item is NOT deleted because it belongs to another group
            var item = await db.QueryFirstOrDefaultAsync<Item>(
                "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
                new { Id = itemId, ItemGroupId = otherGroupId });
            Assert.NotNull(item);
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

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, db, default);

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

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new { Id = itemId, Name = "Item", Description = "Desc", IsComplete = false, ItemGroupId = itemGroupId });

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, db, default);

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
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });

        // Act
        var result = await DeleteItem.Execute(itemGroupId, itemId, claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm item still does not exist
            var deleted = await db.QueryFirstOrDefaultAsync<Item>(
                "SELECT Id, Name, Description, IsComplete, ItemGroupId FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
                new { Id = itemId, ItemGroupId = itemGroupId });
            Assert.Null(deleted);
        }
        else
        {
            Assert.Fail("Expected Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>.");
        }
    }
}

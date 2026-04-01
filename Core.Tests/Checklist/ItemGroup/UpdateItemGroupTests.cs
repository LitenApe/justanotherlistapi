using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

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

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = oldName });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = itemGroupId });

        var request = new UpdateItemGroup.Request { Name = newName };

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, db, default);

        // Assert
        Assert.NotNull(result);
        if (result is Results<NoContent, BadRequest, UnauthorizedHttpResult, ForbidHttpResult> results)
        {
            Assert.IsType<NoContent>(results.Result);

            // Confirm DB update
            var updated = await db.QueryFirstOrDefaultAsync<ItemGroup>(
                "SELECT Id, Name FROM ItemGroups WHERE Id = @Id", new { Id = itemGroupId });
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

        await using var db = await TestDatabase.CreateAsync();

        var request = new UpdateItemGroup.Request { Name = "" };

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, db, default);

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
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, db, default);

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

        await using var db = await TestDatabase.CreateAsync();
        // Add ItemGroup but not Member
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = itemGroupId, Name = "Old Name" });

        var request = new UpdateItemGroup.Request { Name = "New Name" };

        // Act
        var result = await UpdateItemGroup.Execute(itemGroupId, request, claimsPrincipal, db, default);

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

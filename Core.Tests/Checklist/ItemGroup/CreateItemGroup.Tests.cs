using System.Security.Claims;
using Core.AuditLog;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class CreateItemGroupTests
{
    [Fact]
    public async Task Execute_ReturnsCreated_WhenUserIsAuthenticated()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        var userId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult> result =
            await CreateItemGroup.Execute(request, claimsPrincipal, db, new AuditContext());

        // Assert
        Created<ItemGroup> createdResult = Assert.IsType<Created<ItemGroup>>(result.Result);
        ItemGroup? itemGroup = createdResult.Value;
        Assert.NotNull(itemGroup);
        Assert.Equal("Test Group", itemGroup.Name);
        Assert.Equal($"/list/{itemGroup.Id}", createdResult.Location);
    }

    [Fact]
    public async Task Execute_CreateNewDataEntry_WhenRequestNameExist()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        var userId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult> result =
            await CreateItemGroup.Execute(request, claimsPrincipal, db, new AuditContext());

        // Assert
        Created<ItemGroup> createdResult = Assert.IsType<Created<ItemGroup>>(result.Result);
        ItemGroup? itemGroup = createdResult.Value;
        Assert.NotNull(itemGroup);

        ItemGroup? dataEntry = await db.QueryFirstOrDefaultAsync<ItemGroup>(
            "SELECT Id, Name FROM ItemGroups WHERE Id = @Id",
            new { itemGroup.Id }
        );

        Assert.NotNull(dataEntry);
        Assert.Equal(request.Name, dataEntry.Name);
        Assert.Equal($"/list/{dataEntry.Id}", createdResult.Location);
    }

    [Fact]
    public async Task Execute_AddUserAsMemberOfItemGroup_WhenItemGroupIsCreated()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        var userId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult> result =
            await CreateItemGroup.Execute(request, claimsPrincipal, db, new AuditContext());

        // Assert
        Created<ItemGroup> createdResult = Assert.IsType<Created<ItemGroup>>(result.Result);
        ItemGroup? itemGroup = createdResult.Value;
        Assert.NotNull(itemGroup);

        Guid? dataEntry = await db.QueryFirstOrDefaultAsync<Guid?>(
            "SELECT MemberId FROM Members WHERE MemberId = @MemberId AND ItemGroupId = @ItemGroupId",
            new { MemberId = userId, ItemGroupId = itemGroup.Id }
        );

        Assert.NotNull(dataEntry);
        Assert.Single(itemGroup.Members);
        Assert.Equal(userId, itemGroup.Members[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_BadRequest_WhenRequestNameIsEmptyOrWhitespace(string name)
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = name };
        var userId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult> result =
            await CreateItemGroup.Execute(request, claimsPrincipal, db, new AuditContext());

        // Assert
        Assert.IsType<BadRequest>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity()
        );

        await using SqliteConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<Created<ItemGroup>, BadRequest, UnauthorizedHttpResult> result =
            await CreateItemGroup.Execute(request, claimsPrincipal, db, new AuditContext());

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result.Result);
    }
}

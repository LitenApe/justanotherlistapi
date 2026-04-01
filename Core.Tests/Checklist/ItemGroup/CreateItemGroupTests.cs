using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

public class CreateItemGroupTests
{
    [Fact]
    public async Task Execute_ReturnsCreated_WhenUserIsAuthenticated()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        var userId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await CreateItemGroup.Execute(request, claimsPrincipal, db);

        // Assert
        var createdResult = Assert.IsType<Created<ItemGroup>>(result.Result);
        var itemGroup = createdResult.Value;
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
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await CreateItemGroup.Execute(request, claimsPrincipal, db);

        // Assert
        var createdResult = Assert.IsType<Created<ItemGroup>>(result.Result);
        var itemGroup = createdResult.Value;
        Assert.NotNull(itemGroup);

        var dataEntry = await db.QueryFirstOrDefaultAsync<ItemGroup>(
            "SELECT Id, Name FROM ItemGroups WHERE Id = @Id", new { itemGroup.Id });

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
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await CreateItemGroup.Execute(request, claimsPrincipal, db);

        // Assert
        var createdResult = Assert.IsType<Created<ItemGroup>>(result.Result);
        var itemGroup = createdResult.Value;
        Assert.NotNull(itemGroup);

        var dataEntry = await db.QueryFirstOrDefaultAsync<Member>(
            "SELECT MemberId, ItemGroupId FROM Members WHERE MemberId = @MemberId AND ItemGroupId = @ItemGroupId",
            new { MemberId = userId, ItemGroupId = itemGroup.Id });

        Assert.NotNull(dataEntry);
        Assert.Single(itemGroup.Members);
        Assert.Equal(userId, itemGroup.Members[0].MemberId);
        Assert.Equal(itemGroup.Id, itemGroup.Members[0].ItemGroupId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_BadRequest_WhenRequestNameIsEmptyOrWhitespace(string name)
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = name };
        var userId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await CreateItemGroup.Execute(request, claimsPrincipal, db);

        // Assert
        Assert.IsType<BadRequest>(result.Result);
    }
}

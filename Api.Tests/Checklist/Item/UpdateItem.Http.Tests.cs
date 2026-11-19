using System.Net;
using Checklist.Domain;
using Dapper;

namespace Api.Tests.Checklist.ItemTests;

public sealed class UpdateItemHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsNoContent_OnHappyPath()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Test Group" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        var itemId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "Test Item",
                Description = (string?)null,
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}/{itemId}",
            new { Name = "Updated Item", IsComplete = true },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MapEndpoint_ReturnsBadRequest_AndKeepsTheItem_WhenTheNameIsBlank(string name)
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        await factory.Connection.InsertItem(itemGroupId, itemId, "Test Item");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}/{itemId}",
            new { Name = name, IsComplete = true },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Item? item = await factory.Connection.LoadItem(itemId);
        Assert.NotNull(item);
        Assert.Equal("Test Item", item.Name);
        Assert.False(item.IsComplete);
    }

    [Fact]
    public async Task MapEndpoint_ReturnsForbidden_AndKeepsTheItem_WhenTheUserIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        await factory.Connection.InsertItem(itemGroupId, itemId, "Test Item");
        HttpClient client = factory.CreateClientFor(Guid.NewGuid());

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}/{itemId}",
            new { Name = "Updated Item", IsComplete = true },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Test Item", (await factory.Connection.LoadItem(itemId))?.Name);
    }
}

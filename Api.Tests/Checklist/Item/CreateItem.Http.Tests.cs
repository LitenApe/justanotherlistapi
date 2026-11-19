using System.Net;
using Dapper;

namespace Api.Tests.Checklist.ItemTests;

public sealed class CreateItemHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsCreated_OnHappyPath()
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

        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "My Item" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MapEndpoint_ReturnsBadRequest_AndAddsNothing_WhenTheNameIsBlank(string name)
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = name },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountItems(itemGroupId));
    }

    [Fact]
    public async Task MapEndpoint_ReturnsForbidden_AndAddsNothing_WhenTheUserIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        HttpClient client = factory.CreateClientFor(Guid.NewGuid());

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "My Item" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountItems(itemGroupId));
    }

    private Task<int> CountItems(Guid itemGroupId) =>
        factory.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Items WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId }
        );
}

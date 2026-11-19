using System.Net;
using Dapper;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class UpdateItemGroupHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "Updated Group" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MapEndpoint_ReturnsBadRequest_AndKeepsTheName_WhenTheNameIsBlank(string name)
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = name },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Group", await factory.Connection.LoadItemGroupName(itemGroupId));
    }

    [Fact]
    public async Task MapEndpoint_ReturnsForbidden_AndKeepsTheName_WhenTheUserIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        HttpClient client = factory.CreateClientFor(Guid.NewGuid());

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "Renamed" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Group", await factory.Connection.LoadItemGroupName(itemGroupId));
    }
}

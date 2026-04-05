using System.Net;
using Dapper;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class GetItemGroupsHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsOk_OnHappyPath()
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

        HttpResponseMessage response = await client.GetAsync("/api/list");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

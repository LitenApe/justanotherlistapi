using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace Core.Tests.Checklist.ItemTests;

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

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "My Item" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

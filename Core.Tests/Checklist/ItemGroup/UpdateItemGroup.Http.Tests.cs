using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace Core.Tests.Checklist.ItemGroupTests;

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
            new { Name = "Updated Group" }
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

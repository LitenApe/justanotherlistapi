using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace Core.Tests.Checklist.ItemTests;

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
            new { Name = "Updated Item", IsComplete = true }
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

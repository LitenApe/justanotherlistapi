using System.Net;
using System.Net.Http.Json;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class CreateItemGroupHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsCreated_OnHappyPath()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/list",
            new { Name = "My Group" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

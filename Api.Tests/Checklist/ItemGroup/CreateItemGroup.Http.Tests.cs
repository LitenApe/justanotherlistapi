using System.Net;
using Dapper;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class CreateItemGroupHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsCreated_OnHappyPath()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/list",
            new { Name = "My Group" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MapEndpoint_ReturnsBadRequest_AndCreatesNothing_WhenTheNameIsBlank(
        string name
    )
    {
        var userId = Guid.NewGuid();
        HttpClient client = factory.CreateClientFor(userId);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/list",
            new { Name = name },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            0,
            await factory.Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Members WHERE MemberId = @MemberId",
                new { MemberId = userId }
            )
        );
    }
}

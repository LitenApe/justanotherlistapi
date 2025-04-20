using System.Net;
using System.Net.Http.Json;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Mvc.Testing;

public class CreateItemGroupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CreateItemGroupTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Execute_ReturnsCreated_WhenUserIsAuthorized()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Set a timeout

        // Act
        var response = await _client.PostAsJsonAsync("/api/list", request, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdItemGroup = await response.Content.ReadFromJsonAsync<ItemGroup>(cancellationToken: cts.Token);
        Assert.NotNull(createdItemGroup);
        Assert.Equal("Test Group", createdItemGroup.Name);
    }

    [Fact]
    public async Task Execute_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "" };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var response = await _client.PostAsJsonAsync("/api/list", request, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Remove Authorization header
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsJsonAsync("/api/list", request, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

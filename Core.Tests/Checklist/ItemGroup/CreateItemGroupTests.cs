using System.Net;
using System.Net.Http.Json;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class CreateItemGroupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CreateItemGroupTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Execute_ReturnsCreated_WhenUserIsAuthorized()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Set a timeout

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/list", request, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdItemGroup = await response.Content.ReadFromJsonAsync<ItemGroup>(cancellationToken: cts.Token);
        Assert.NotNull(createdItemGroup);
        Assert.Equal("Test Group", createdItemGroup.Name);
    }

    [Fact]
    public async Task Execute_WritesToDatabase_WhenUserIsAuthorized()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Set a timeout

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/list", request, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify the database write
        using var scope = _factory.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var createdItemGroup = await dbContext.ItemGroups.FirstOrDefaultAsync(ig => ig.Name == "Test Group", cts.Token);
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
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/list", request, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/list", request, cts.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

﻿using System.Security.Claims;
using Core;
using Core.Checklist;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public class CreateItemGroupTests
{
    [Fact]
    public async Task Execute_ReturnsCreated_WhenUserIsAuthenticated()
    {
        // Arrange
        var request = new CreateItemGroup.Request { Name = "Test Group" };
        var userId = Guid.NewGuid();

        // Mock ClaimsPrincipal
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        // Mock DatabaseContext
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;
        await using var dbContext = new DatabaseContext(options);

        // Act
        var result = await CreateItemGroup.Execute(request, claimsPrincipal, dbContext);

        // Assert
        Assert.NotNull(result);
        if (result is Results<Created<ItemGroup>, UnauthorizedHttpResult> results)
        {
            if (results.Result is Created<ItemGroup> createdResult)
            {
                var itemGroup = createdResult.Value;
                Assert.NotNull(itemGroup);
                Assert.Equal("Test Group", itemGroup.Name);
                Assert.Equal($"/list/{itemGroup.Id}", createdResult.Location);
            }
            else
            {
                Assert.Fail("Expected a Created<ItemGroup> result.");
            }
        }
        else
        {
            Assert.Fail("Expected a Results<Created<ItemGroup>, UnauthorizedHttpResult> result.");
        }
    }
}

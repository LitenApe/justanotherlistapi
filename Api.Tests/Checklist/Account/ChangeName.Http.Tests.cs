using System.Net;
using Checklist.Domain;
using Dapper;

namespace Api.Tests.Checklist.AccountTests;

public sealed class ChangeNameHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task PutMe_RenamesTheSignedInUser_AndReturnsThem()
    {
        var userId = Guid.NewGuid();
        string email = TestAccounts.UniqueEmail();
        await factory.Connection.InsertUser(userId, "Kim", email);

        HttpResponseMessage response = await factory
            .CreateClientFor(userId)
            .PutAsJsonAsync(
                "/api/me",
                new { Name = "  Kim Lee " },
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new User
            {
                Id = userId,
                Name = "Kim Lee",
                Email = email,
            },
            await response.Content.ReadFromJsonAsync<User>(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            "Kim Lee",
            await factory.Connection.ExecuteScalarAsync<string>(
                "SELECT Name FROM Users WHERE Id = @Id",
                new { Id = userId }
            )
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task PutMe_Returns400_ForANameThatBreaksTheRule(string? name)
    {
        HttpResponseMessage response = await factory
            .CreateClient()
            .PutAsJsonAsync("/api/me", new { Name = name }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_Returns404_ForSomeoneWithoutAnAccount()
    {
        HttpResponseMessage response = await factory
            .CreateClientFor(Guid.NewGuid())
            .PutAsJsonAsync("/api/me", new { Name = "Kim" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_Returns401_WithoutAToken()
    {
        HttpResponseMessage response = await factory
            .CreateAnonymousClient()
            .PutAsJsonAsync("/api/me", new { Name = "Kim" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

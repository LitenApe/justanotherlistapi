using System.Net;
using System.Text.Json;
using Api.Http;
using Dapper;

namespace Api.Tests.Checklist.AccountTests;

public sealed class SignUpHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task PostSignUp_Returns201WithASession_ForTheNewAccount()
    {
        string email = TestAccounts.UniqueEmail();

        HttpResponseMessage response = await factory
            .CreateAnonymousClient()
            .PostAsJsonAsync(
                "/api/auth/sign-up",
                new
                {
                    Name = "  Kim  ",
                    Email = email,
                    Password = TestAccounts.KnownPassword,
                },
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        SessionResponse? session = await response.Content.ReadFromJsonAsync<SessionResponse>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(session);
        Assert.False(string.IsNullOrEmpty(session.AccessToken));
        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow.AddHours(7));
        Assert.Equal(("Kim", email), (session.User.Name, session.User.Email));
        Assert.Equal(
            "Kim",
            await factory.Connection.ExecuteScalarAsync<string>(
                "SELECT Name FROM Users WHERE Id = @Id",
                new { session.User.Id }
            )
        );
    }

    [Fact]
    public async Task PostSignUp_Returns400_NamingEveryFieldThatBreaksItsRule()
    {
        HttpResponseMessage response = await factory
            .CreateAnonymousClient()
            .PostAsJsonAsync(
                "/api/auth/sign-up",
                new
                {
                    Name = " ",
                    Email = "not an email",
                    Password = "short",
                },
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            ["email", "name", "password"],
            problem
                .RootElement.GetProperty("errors")
                .EnumerateObject()
                .Select(field => field.Name)
                .Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task PostSignUp_Returns409_WhenAnotherAccountUsesTheEmail_IgnoringCase()
    {
        string email = TestAccounts.UniqueEmail();
        HttpClient client = factory.CreateAnonymousClient();
        await SignUp(client, email);

        HttpResponseMessage response = await SignUp(client, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Two people sign up with one email at the same moment. One gets the account; the other
    // gets 409, never a server error from the unique index. Each round races the two once.
    [Fact]
    public async Task PostSignUp_CreatesOneAccount_WhenTwoSignUpsUseOneEmailAtOnce()
    {
        HttpClient client = factory.CreateAnonymousClient();

        for (int round = 0; round < 10; round++)
        {
            string email = TestAccounts.UniqueEmail();

            HttpResponseMessage[] responses = await Task.WhenAll(
                SignUp(client, email),
                SignUp(client, email.ToUpperInvariant())
            );

            Assert.Equal(
                new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
                responses.Select(r => r.StatusCode).Order()
            );
        }
    }

    private static Task<HttpResponseMessage> SignUp(HttpClient client, string email) =>
        client.PostAsJsonAsync(
            "/api/auth/sign-up",
            new
            {
                Name = "Kim",
                Email = email,
                Password = TestAccounts.KnownPassword,
            },
            TestContext.Current.CancellationToken
        );
}

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Api.Accounts;
using Api.Http;
using Checklist.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests.Checklist.AccountTests;

/// <summary>
/// The tokens the API issues are the ones it accepts, and only those: these tests switch the
/// test host from <see cref="TestAuthHandler"/> back to the app's own JWT validation.
/// </summary>
public sealed class AccessTokenHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task TheTokenFromSigningUp_OpensTheListEndpoints()
    {
        await using WebApplicationFactory<Program> app = WithBearerTokens();
        HttpClient client = app.CreateClient();
        SessionResponse session = await SignUp(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session.AccessToken
        );
        HttpResponseMessage response = await client.GetAsync(
            "/api/list",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsRefused()
    {
        await using WebApplicationFactory<Program> app = WithBearerTokens();
        HttpClient client = app.CreateClient();
        SessionResponse session = await SignUp(client);
        AccessToken forged = Issue(
            session.User,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(AuthOptions.MinKeyBytes)),
            TimeProvider.System
        );

        HttpResponseMessage response = await GetListsWith(client, forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnExpiredToken_IsRefused()
    {
        await using WebApplicationFactory<Program> app = WithBearerTokens();
        HttpClient client = app.CreateClient();
        SessionResponse session = await SignUp(client);
        // Issued with the app's key two days ago, so it ran out yesterday.
        AccessToken expired = Issue(
            session.User,
            ApiFactory.SigningKey,
            new FixedTimeProvider(DateTimeOffset.UtcNow.AddDays(-2))
        );

        HttpResponseMessage response = await GetListsWith(client, expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> WithBearerTokens() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
            )
        );

    private static AccessToken Issue(User user, string signingKey, TimeProvider clock) =>
        new JwtTokenIssuer(
            Options.Create(new AuthOptions { SigningKey = signingKey }),
            clock
        ).Issue(user);

    private static Task<HttpResponseMessage> GetListsWith(HttpClient client, AccessToken token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.Value
        );
        return client.GetAsync("/api/list", TestContext.Current.CancellationToken);
    }

    private static async Task<SessionResponse> SignUp(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/sign-up",
            new
            {
                Name = "Kim",
                Email = TestAccounts.UniqueEmail(),
                Password = TestAccounts.KnownPassword,
            },
            TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();
        return (
            await response.Content.ReadFromJsonAsync<SessionResponse>(
                TestContext.Current.CancellationToken
            )
        )!;
    }
}

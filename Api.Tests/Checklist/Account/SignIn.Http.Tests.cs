using System.Net;
using Api.Http;

namespace Api.Tests.Checklist.AccountTests;

public sealed class SignInHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task PostSignIn_Returns200WithASession_ForTheRightPassword()
    {
        HttpClient client = factory.CreateAnonymousClient();
        string email = TestAccounts.UniqueEmail();
        SessionResponse signedUp = await SignUp(client, email);

        HttpResponseMessage response = await SignIn(
            client,
            email.ToUpperInvariant(),
            TestAccounts.KnownPassword
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SessionResponse? session = await response.Content.ReadFromJsonAsync<SessionResponse>(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(signedUp.User, session?.User);
        Assert.False(string.IsNullOrEmpty(session?.AccessToken));
    }

    [Fact]
    public async Task PostSignIn_Returns401_ForAWrongPasswordAndForAnEmailWithoutAnAccount()
    {
        HttpClient client = factory.CreateAnonymousClient();
        string email = TestAccounts.UniqueEmail();
        await SignUp(client, email);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SignIn(client, email, "not the password")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (
                await SignIn(client, TestAccounts.UniqueEmail(), TestAccounts.KnownPassword)
            ).StatusCode
        );
    }

    [Theory]
    [InlineData(null, "a password")]
    [InlineData("not an email", "a password")]
    [InlineData("kim@example.com", "")]
    [InlineData("kim@example.com", null)]
    public async Task PostSignIn_Returns400_WithoutAnEmailAndAPassword(
        string? email,
        string? password
    )
    {
        HttpResponseMessage response = await SignIn(
            factory.CreateAnonymousClient(),
            email,
            password
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSignIn_Returns423_OnceFiveWrongPasswordsLockTheAccountOut()
    {
        HttpClient client = factory.CreateAnonymousClient();
        string email = TestAccounts.UniqueEmail();
        await SignUp(client, email);

        for (int attempt = 1; attempt < 5; attempt++)
        {
            await SignIn(client, email, "not the password");
        }

        Assert.Equal(
            HttpStatusCode.Locked,
            (await SignIn(client, email, "not the password")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Locked,
            (await SignIn(client, email, TestAccounts.KnownPassword)).StatusCode
        );
    }

    private static async Task<SessionResponse> SignUp(HttpClient client, string email)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/sign-up",
            new
            {
                Name = "Kim",
                Email = email,
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

    private static Task<HttpResponseMessage> SignIn(
        HttpClient client,
        string? email,
        string? password
    ) =>
        client.PostAsJsonAsync(
            "/api/auth/sign-in",
            new { Email = email, Password = password },
            TestContext.Current.CancellationToken
        );
}

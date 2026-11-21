using System.Net;
using System.Text.Json;

namespace Api.Tests.Checklist.AccountTests;

public sealed class PasswordResetHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string NewPassword = "a brand new passphrase";

    [Fact]
    public async Task ForgotPassword_Answers202Either_WayAndEmailsOnlyRealAccounts()
    {
        HttpClient client = factory.CreateAnonymousClient();
        string email = TestAccounts.UniqueEmail();
        await SignUp(client, email);
        int sentBefore = factory.Mail.PasswordResets.Count;

        HttpResponseMessage known = await ForgotPassword(client, email);
        HttpResponseMessage unknown = await ForgotPassword(client, TestAccounts.UniqueEmail());

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(sentBefore + 1, factory.Mail.PasswordResets.Count);
        Assert.Equal(email, factory.Mail.PasswordResets.Last().User.Email);
    }

    [Fact]
    public async Task ForgotPassword_Returns400_ForSomethingThatIsntAnEmail()
    {
        HttpResponseMessage response = await ForgotPassword(
            factory.CreateAnonymousClient(),
            "not an email"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_SetsTheNewPassword_Once()
    {
        HttpClient client = factory.CreateAnonymousClient();
        string email = TestAccounts.UniqueEmail();
        Guid userId = await SignUp(client, email);
        await ForgotPassword(client, email);
        string code = factory.Mail.LastCodeFor(email);

        HttpResponseMessage reset = await ResetPassword(client, userId, code, NewPassword);
        HttpResponseMessage again = await ResetPassword(client, userId, code, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(["code"], await ProblemFields(again));
        Assert.Equal(HttpStatusCode.OK, (await SignIn(client, email, NewPassword)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await SignIn(client, email, TestAccounts.KnownPassword)).StatusCode
        );
    }

    [Fact]
    public async Task ResetPassword_Returns400_NamingTheCodeAndPasswordProblems()
    {
        HttpClient client = factory.CreateAnonymousClient();

        HttpResponseMessage incomplete = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { Password = "short" },
            TestContext.Current.CancellationToken
        );
        HttpResponseMessage wrongCode = await ResetPassword(
            client,
            Guid.NewGuid(),
            "bm90LWEtY29kZQ",
            NewPassword
        );

        Assert.Equal(["code", "password"], await ProblemFields(incomplete));
        Assert.Equal(["code"], await ProblemFields(wrongCode));
    }

    private static async Task<string[]> ProblemFields(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        return
        [
            .. problem
                .RootElement.GetProperty("errors")
                .EnumerateObject()
                .Select(field => field.Name)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static async Task<Guid> SignUp(HttpClient client, string email)
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
            await response.Content.ReadFromJsonAsync<Api.Http.SessionResponse>(
                TestContext.Current.CancellationToken
            )
        )!
            .User
            .Id;
    }

    private static Task<HttpResponseMessage> ForgotPassword(HttpClient client, string email) =>
        client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { Email = email },
            TestContext.Current.CancellationToken
        );

    private static Task<HttpResponseMessage> ResetPassword(
        HttpClient client,
        Guid userId,
        string code,
        string password
    ) =>
        client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new
            {
                UserId = userId,
                Code = code,
                Password = password,
            },
            TestContext.Current.CancellationToken
        );

    private static Task<HttpResponseMessage> SignIn(
        HttpClient client,
        string email,
        string password
    ) =>
        client.PostAsJsonAsync(
            "/api/auth/sign-in",
            new { Email = email, Password = password },
            TestContext.Current.CancellationToken
        );
}

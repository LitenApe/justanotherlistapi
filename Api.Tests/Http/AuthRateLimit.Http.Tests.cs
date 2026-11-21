using System.Net;
using Api.AuditLog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests.HttpTests;

public sealed class AuthRateLimitHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task AccountEndpoints_Answer429_OverTheLimit_AndTheAuditLogSaysSo()
    {
        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> limited = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:RateLimit:PermitLimit", "2");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            });
        });
        HttpClient client = limited.CreateClient();

        HttpStatusCode[] statuses = new HttpStatusCode[3];
        for (int attempt = 0; attempt < statuses.Length; attempt++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/auth/sign-in",
                new { Email = "kim@example.com", Password = "a guess at it" },
                TestContext.Current.CancellationToken
            );
            statuses[attempt] = response.StatusCode;
        }

        Assert.Equal(
            [
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests,
            ],
            statuses
        );
        Assert.Equal(
            ["InvalidCredentials", "InvalidCredentials", "TooManyRequests"],
            writer.Entries.Select(entry => entry.Outcome),
            StringComparer.Ordinal
        );
        AuditEntry rejected = writer.Entries[^1];
        Assert.Equal(("SignIn", "Account"), (rejected.Operation, rejected.ResourceType));
    }
}

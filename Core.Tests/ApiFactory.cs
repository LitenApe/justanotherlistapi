using System.Data;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Core.AuditLog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection connection = null!;

    public static Guid TestUserId => TestAuthHandler.UserId;

    public SqliteConnection Connection => connection;

    async Task IAsyncLifetime.InitializeAsync()
    {
        connection = await TestDatabase.CreateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ConnectionStrings:database"] = "Server=localhost;Database=test",
                }
            )
        );

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbConnection>();
            services.AddSingleton<IDbConnection>(connection);

            // Replace IAuditWriter with a no-op so no SQL Server connections are made during tests.
            // ChannelAuditWriter remains registered and its IHostedService starts normally,
            // but since IAuditWriter resolves to NoOpAuditWriter, no entries are ever enqueued.
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton<IAuditWriter, NoOpAuditWriter>();

            services
                .AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = "Test";
                opts.DefaultChallengeScheme = "Test";
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            connection?.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims = [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())];
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

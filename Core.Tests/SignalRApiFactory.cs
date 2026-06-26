using System.Data;
using Core.AuditLog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.Tests;

public sealed class SignalRApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqlConnection connection = null!;

    public SqlConnection Connection => connection;

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

            services.RemoveAll<IAuditWriter>();
            services.AddSingleton<IAuditWriter, NoOpAuditWriter>();

            // Keep real IChecklistNotifier (ChecklistNotifier) and SignalR hub
            // so we can test the full notification pipeline.

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

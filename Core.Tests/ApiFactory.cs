using System.Security.Claims;
using System.Text.Encodings.Web;
using Core.AuditLog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Tests;

/// <summary>
/// Runs the whole app against an isolated test database. Requests get the app's own scoped
/// connection, closed until Dapper opens it, exactly as in production. The real
/// <c>ChecklistNotifier</c> and hub stay in place, so SignalR tests see real broadcasts.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string connectionString = null!;
    private SqlConnection connection = null!;

    public static Guid TestUserId => TestAuthHandler.UserId;

    /// <summary>An open connection to the app's database, for arranging data and checking results.</summary>
    public SqlConnection Connection => connection;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        connectionString = await TestDatabase.CreateConnectionStringAsync();
        connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
    }

    /// <summary>A client whose requests are made as <paramref name="userId"/>.</summary>
    public HttpClient CreateClientFor(Guid userId)
    {
        return CreateClientWithUser(userId.ToString());
    }

    /// <summary>A client whose requests aren't authenticated.</summary>
    public HttpClient CreateAnonymousClient()
    {
        return CreateClientWithUser(TestAuthHandler.Anonymous);
    }

    /// <summary>A connection to the checklist hub, as <paramref name="userId"/> or <see cref="TestUserId"/>.</summary>
    public HubConnection CreateHubConnection(Guid? userId = null)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                $"{Server.BaseAddress}hubs/checklist",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                    options.Headers[TestAuthHandler.UserHeader] = (userId ?? TestUserId).ToString();
                }
            )
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // A host setting, so it's already in the configuration when Program.cs registers the
        // app's SqlConnection (ApiFactoryTests checks this).
        builder.UseSetting("ConnectionStrings:database", connectionString);

        builder.ConfigureServices(services =>
        {
            // Replace IAuditWriter with a no-op so audit entries don't end up in the database.
            // ChannelAuditWriter remains registered and its IHostedService starts normally,
            // but since IAuditWriter resolves to NoOpAuditWriter, no entries are ever enqueued.
            services.RemoveAll<IAuditWriter>();
            services.AddSingleton<IAuditWriter, NoOpAuditWriter>();

            services.RemoveChecklistCleanupService();

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

    private HttpClient CreateClientWithUser(string user)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        return client;
    }
}

/// <summary>
/// Authenticates each request as the user in the <see cref="UserHeader"/> header, or as
/// <see cref="UserId"/> without one. <see cref="Anonymous"/> leaves the request unauthenticated.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string UserHeader = "X-Test-User";
    public const string Anonymous = "anonymous";

    public static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? user = Request.Headers[UserHeader];
        if (string.Equals(user, Anonymous, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string userId = user ?? UserId.ToString();
        Claim[] claims = [new Claim(ClaimTypes.NameIdentifier, userId)];
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

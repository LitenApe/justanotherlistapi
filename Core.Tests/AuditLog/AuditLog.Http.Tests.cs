using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Core.AuditLog;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Tests.AuditLog;

public sealed class AuditLogHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CreateItemGroup_CapturesAuditEntry_WithResourceId()
    {
        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/list",
            new { Name = "My Group" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("CreateItemGroup", entry.Operation);
        Assert.Equal("ItemGroup", entry.ResourceType);
        Assert.Equal("Success", entry.Outcome);
        Assert.NotNull(entry.ResourceId);
        Assert.Equal(ApiFactory.TestUserId, entry.UserId);
    }

    [Fact]
    public async Task CreateItem_CapturesAuditEntry_WithSubResourceIdAndResourceId()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Test Group" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "My Item" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("CreateItem", entry.Operation);
        Assert.Equal("Item", entry.ResourceType);
        Assert.Equal("Success", entry.Outcome);
        Assert.Equal(itemGroupId, entry.ResourceId);
        Assert.NotNull(entry.SubResourceId);
    }

    [Fact]
    public async Task DeleteItemGroup_CapturesAuditEntry_OnSuccess()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "My Precious Group" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.DeleteAsync($"/api/list/{itemGroupId}");

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("DeleteItemGroup", entry.Operation);
        Assert.Equal("Success", entry.Outcome);
        Assert.Equal(itemGroupId, entry.ResourceId);
    }

    [Fact]
    public async Task UpdateItemGroup_CapturesAuditEntry_OnSuccess()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Old Name" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.PutAsJsonAsync($"/api/list/{itemGroupId}", new { Name = "New Name" });

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("UpdateItemGroup", entry.Operation);
        Assert.Equal("Success", entry.Outcome);
    }

    [Fact]
    public async Task AddMember_CapturesAuditEntry_WithTargetUserId()
    {
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Test Group" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.PostAsync($"/api/list/{itemGroupId}/member/{newMemberId}", null);

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("AddMember", entry.Operation);
        Assert.Equal("Success", entry.Outcome);
        Assert.Equal(newMemberId, entry.TargetUserId);
        Assert.Equal(itemGroupId, entry.ResourceId);
    }

    [Fact]
    public async Task GetItemGroup_CapturesForbiddenOutcome_WhenUserIsNotMember()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Forbidden Group" }
        );

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.GetAsync($"/api/list/{itemGroupId}");

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("GetItemGroup", entry.Operation);
        Assert.Equal("Forbidden", entry.Outcome);
        Assert.Equal(ApiFactory.TestUserId, entry.UserId);
    }

    [Fact]
    public async Task UpdateItemGroup_CapturesBadRequestOutcome_WhenNameIsBlank()
    {
        var itemGroupId = Guid.NewGuid();

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.PutAsJsonAsync($"/api/list/{itemGroupId}", new { Name = "" });

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("UpdateItemGroup", entry.Operation);
        Assert.Equal("BadRequest", entry.Outcome);
        Assert.Equal(itemGroupId, entry.ResourceId);
    }

    [Fact]
    public async Task GetItemGroup_CapturesNotFoundOutcome_WhenGroupDoesNotExist()
    {
        // Microsoft.Data.Sqlite 10.x enables FK constraints by default at the library level;
        // they cannot be disabled via PRAGMA after the connection is open. We use a separate
        // connection with Foreign Keys=False so we can insert an orphaned Members row (user is
        // a member but the ItemGroups row is absent — the GetItemGroup 404 branch).
        var itemGroupId = Guid.NewGuid();
        await using var noFkConnection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=False"
        );
        await noFkConnection.OpenAsync();
        await TestDatabase.CreateTablesAsync(noFkConnection);
        await noFkConnection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IDbConnection>();
                services.AddSingleton<IDbConnection>((IDbConnection)noFkConnection);
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.GetAsync($"/api/list/{itemGroupId}");

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("GetItemGroup", entry.Operation);
        Assert.Equal("NotFound", entry.Outcome);
        Assert.Equal(itemGroupId, entry.ResourceId);
    }

    [Fact]
    public async Task AddMember_CapturesConflictOutcome_WhenUserIsAlreadyMember()
    {
        var itemGroupId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Test Group" }
        );
        await factory.Connection.ExecuteAsync(
            """
            INSERT INTO Members (MemberId, ItemGroupId) VALUES (@ActorId, @ItemGroupId);
            INSERT INTO Members (MemberId, ItemGroupId) VALUES (@TargetId, @ItemGroupId);
            """,
            new
            {
                ActorId = TestAuthHandler.UserId,
                TargetId = existingMemberId,
                ItemGroupId = itemGroupId,
            }
        );

        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.PostAsync($"/api/list/{itemGroupId}/member/{existingMemberId}", null);

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("AddMember", entry.Operation);
        Assert.Equal("Conflict", entry.Outcome);
        Assert.Equal(existingMemberId, entry.TargetUserId);
        Assert.Equal(itemGroupId, entry.ResourceId);
    }

    [Fact]
    public async Task CreateItemGroup_CapturesMissingClaimOutcome_WhenUserIdClaimIsAbsent()
    {
        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
                // Replace the Test scheme handler with one that authenticates but provides no
                // identifier claims — simulates a valid token where sub/user_id is absent.
                services.PostConfigure<AuthenticationOptions>(options =>
                    options.SchemeMap["Test"].HandlerType = typeof(NoClaimAuthHandler)
                );
            })
        );
        HttpClient client = webFactory.CreateClient();

        await client.PostAsJsonAsync("/api/list", new { Name = "A Group" });

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("CreateItemGroup", entry.Operation);
        Assert.Equal("MissingClaim", entry.Outcome);
        Assert.Equal("Required user identifier claim is missing.", entry.FailureReason);
        Assert.Null(entry.UserId);
    }

    [Fact]
    public async Task Request_CapturesAuthenticationFailedOutcome_WhenBearerTokenIsInvalid()
    {
        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
                // Override the default scheme to Bearer so the JWT handler processes the token.
                // The ApiFactory sets "Test" as default; this PostConfigure runs after and overrides it.
                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = "Bearer";
                    opts.DefaultChallengeScheme = "Bearer";
                });
            })
        );
        HttpClient client = webFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "invalid.jwt.token"
        );

        await client.GetAsync("/api/list");

        // RequireAuthorization() blocks the endpoint pipeline before the AuditEndpointFilter runs,
        // so the only entry is the one written directly by OnAuthenticationFailed in Program.cs.
        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("AuthenticationFailed", entry.Operation);
        Assert.Equal("AuthenticationFailed", entry.Outcome);
        Assert.NotNull(entry.FailureReason);
        Assert.Null(entry.UserId);
        Assert.Null(entry.ResourceType);
    }
}

// Authenticates successfully but returns a principal with no identifier claims.
// Used to trigger the MissingClaim audit outcome without manipulating JWT tokens.
internal sealed class NoClaimAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([], "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

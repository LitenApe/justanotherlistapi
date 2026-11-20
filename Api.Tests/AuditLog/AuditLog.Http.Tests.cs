using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Api.AuditLog;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Tests.AuditLog;

public sealed class AuditLogHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid auditedGroupId = Guid.NewGuid();
    private static readonly Guid auditedItemId = Guid.NewGuid();
    private static readonly Guid auditedMemberId = Guid.NewGuid();

    // Every endpoint under /api/list, with the operation name and resource type its audit entry
    // must carry. AuditLog.Operation stores the names, so renaming one (or the class its
    // nameof() refers to) would split that operation's history in two.
    private static readonly AuditedEndpoint[] auditedEndpoints =
    [
        new("GetItemGroups", "ItemGroup", HttpMethod.Get, "/api/list"),
        new("GetItemGroup", "ItemGroup", HttpMethod.Get, $"/api/list/{auditedGroupId}"),
        new("CreateItemGroup", "ItemGroup", HttpMethod.Post, "/api/list", new { Name = "" }),
        new(
            "UpdateItemGroup",
            "ItemGroup",
            HttpMethod.Put,
            $"/api/list/{auditedGroupId}",
            new { Name = "Name" }
        ),
        new(
            "UpdateItemGroupSettings",
            "ItemGroup",
            HttpMethod.Put,
            $"/api/list/{auditedGroupId}/settings",
            new { }
        ),
        new("DeleteItemGroup", "ItemGroup", HttpMethod.Delete, $"/api/list/{auditedGroupId}"),
        new(
            "CreateItem",
            "Item",
            HttpMethod.Post,
            $"/api/list/{auditedGroupId}",
            new { Name = "Name" }
        ),
        new(
            "UpdateItem",
            "Item",
            HttpMethod.Put,
            $"/api/list/{auditedGroupId}/{auditedItemId}",
            new { Name = "Name" }
        ),
        new("DeleteItem", "Item", HttpMethod.Delete, $"/api/list/{auditedGroupId}/{auditedItemId}"),
        new(
            "ReserveItem",
            "Item",
            HttpMethod.Put,
            $"/api/list/{auditedGroupId}/{auditedItemId}/reservation",
            new { MemberId = auditedMemberId, Replacing = (Guid?)null }
        ),
        new(
            "UnreserveItem",
            "Item",
            HttpMethod.Delete,
            $"/api/list/{auditedGroupId}/{auditedItemId}/reservation?memberId={auditedMemberId}"
        ),
        new("GetMembers", "ItemGroup", HttpMethod.Get, $"/api/list/{auditedGroupId}/member"),
        new(
            "AddMember",
            "ItemGroup",
            HttpMethod.Post,
            $"/api/list/{auditedGroupId}/member/{auditedMemberId}"
        ),
        new(
            "RemoveMember",
            "ItemGroup",
            HttpMethod.Delete,
            $"/api/list/{auditedGroupId}/member/{auditedMemberId}"
        ),
    ];

    [Fact]
    public async Task EveryEndpoint_CapturesItsOperationResourceTypeAndRouteIds()
    {
        // All requests go through one host: starting a host per endpoint costs seconds each.
        var writer = new CapturingAuditWriter();
        await using WebApplicationFactory<Program> webFactory = WithAuditWriter(writer);
        HttpClient client = webFactory.CreateClient();

        foreach (AuditedEndpoint endpoint in auditedEndpoints)
        {
            writer.Entries.Clear();

            // The caller isn't a member of the group, so most requests end in 403, and
            // CreateItemGroup gets a blank name. Every outcome is audited the same way.
            using var request = new HttpRequestMessage(endpoint.Method, endpoint.Path);
            if (endpoint.Body is not null)
            {
                request.Content = JsonContent.Create(endpoint.Body);
            }
            await client.SendAsync(request, TestContext.Current.CancellationToken);

            // IDs in the request end up in the entry: the group as ResourceId, the item as
            // SubResourceId and the member (in the route, query or body) as TargetUserId.
            // Comparing tuples that start with the operation name makes a failure say which
            // endpoint broke.
            AuditEntry entry = Assert.Single(writer.Entries);
            Assert.Equal(
                (
                    endpoint.Operation,
                    endpoint.ResourceType,
                    IdInRequest(endpoint, auditedGroupId),
                    IdInRequest(endpoint, auditedItemId),
                    IdInRequest(endpoint, auditedMemberId)
                ),
                (
                    entry.Operation,
                    entry.ResourceType,
                    entry.ResourceId,
                    entry.SubResourceId,
                    entry.TargetUserId
                )
            );
        }
    }

    [Fact]
    public void AuditedEndpoints_CoverEveryChecklistEndpoint()
    {
        IEnumerable<string?> mapped = factory
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(e =>
                e.RoutePattern.RawText?.StartsWith("/api/list", StringComparison.Ordinal) == true
            )
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName);

        // A new endpoint fails this until it's added to auditedEndpoints above (and to the
        // resource types in AuditEndpointFilter).
        Assert.Equal(
            auditedEndpoints.Select(e => e.Operation).Order(StringComparer.Ordinal),
            mapped.Order(StringComparer.Ordinal)
        );
    }

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
            new { Name = "My Group" },
            TestContext.Current.CancellationToken
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
            new { Name = "My Item" },
            TestContext.Current.CancellationToken
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

        await client.DeleteAsync($"/api/list/{itemGroupId}", TestContext.Current.CancellationToken);

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

        await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "New Name" },
            TestContext.Current.CancellationToken
        );

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

        await client.PostAsync(
            $"/api/list/{itemGroupId}/member/{newMemberId}",
            null,
            TestContext.Current.CancellationToken
        );

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

        await client.GetAsync($"/api/list/{itemGroupId}", TestContext.Current.CancellationToken);

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

        await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "" },
            TestContext.Current.CancellationToken
        );

        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("UpdateItemGroup", entry.Operation);
        Assert.Equal("BadRequest", entry.Outcome);
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

        await client.PostAsync(
            $"/api/list/{itemGroupId}/member/{existingMemberId}",
            null,
            TestContext.Current.CancellationToken
        );

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

        await client.PostAsJsonAsync(
            "/api/list",
            new { Name = "A Group" },
            TestContext.Current.CancellationToken
        );

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

        await client.GetAsync("/api/list", TestContext.Current.CancellationToken);

        // RequireAuthorization() blocks the endpoint pipeline before the AuditEndpointFilter runs,
        // so the only entry is the one written directly by OnAuthenticationFailed in Program.cs.
        AuditEntry entry = Assert.Single(writer.Entries);
        Assert.Equal("AuthenticationFailed", entry.Operation);
        Assert.Equal("AuthenticationFailed", entry.Outcome);
        Assert.NotNull(entry.FailureReason);
        Assert.Null(entry.UserId);
        Assert.Null(entry.ResourceType);
    }

    private static Guid? IdInRequest(AuditedEndpoint endpoint, Guid id) =>
        endpoint.Path.Contains(id.ToString(), StringComparison.Ordinal)
        || JsonSerializer.Serialize(endpoint.Body).Contains(id.ToString(), StringComparison.Ordinal)
            ? id
            : null;

    private WebApplicationFactory<Program> WithAuditWriter(CapturingAuditWriter writer) =>
        factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditWriter>();
                services.AddSingleton<IAuditWriter>(writer);
            })
        );

    private sealed record AuditedEndpoint(
        string Operation,
        string ResourceType,
        HttpMethod Method,
        string Path,
        object? Body = null
    );
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

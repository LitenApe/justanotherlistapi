# Audit Log Specification

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
  - [Data Flow](#data-flow)
  - [File Structure](#file-structure)
- [Database Schema](#database-schema)
  - [Column Semantics](#column-semantics)
  - [ResourceType Values](#resourcetype-values)
  - [Outcome Values](#outcome-values)
  - [Indexes](#indexes)
  - [Retention](#retention)
- [Per-Operation Audit Data](#per-operation-audit-data)
  - [Checklist — ItemGroup](#checklist--itemgroup)
  - [Checklist — Item](#checklist--item)
  - [Checklist — Member](#checklist--member)
  - [Authentication](#authentication)
- [Implementation](#implementation)
  - [AuditEntry](#auditentry)
  - [IAuditWriter and ChannelAuditWriter](#iauditwriter-and-channelauditwriter)
  - [AuditEndpointFilter](#auditendpointfilter)
  - [AuditContext](#auditcontext)
  - [JWT OnAuthenticationFailed Event](#jwt-onauthenticationfailed-event)
  - [Schema Initializer](#schema-initializer)
  - [Service Registration](#service-registration)
  - [Middleware Order](#middleware-order)
- [Adding Audit Logging to a New Endpoint](#adding-audit-logging-to-a-new-endpoint)
- [Test Infrastructure](#test-infrastructure)
  - [NoOpAuditWriter](#noopauditwriter)
  - [CapturingAuditWriter](#capturingauditwriter)
  - [TestDatabase](#testdatabase)
- [Future Extensibility](#future-extensibility)
  - [Field-Level Change Tracking](#field-level-change-tracking)
  - [Admin UI](#admin-ui)

---

## Overview

Audit logging tracks all attempted and successful operations across the API, including authentication failures, to support security monitoring and attack detection. Every request to the Checklist API produces exactly one audit record — regardless of whether the request succeeds, is rejected by authorisation, or fails validation.

**Guiding constraints:**

- Audit logging must not affect the availability or correctness of normal API functionality.
- Write failures are silently absorbed — no request should fail because the audit log is unavailable.
- The performance impact on request handling must be minimised. Audit writes are non-blocking and decoupled from the request pipeline.
- User-provided free-text content (resource names) is intentionally excluded from audit records to avoid inadvertently logging sensitive data entered by users.

---

## Architecture

### Data Flow

```
HTTP Request
    │
    ▼
RequireAuthorization()      ← JWT validation; failures go directly to OnAuthenticationFailed
    │
    ▼
AuditEndpointFilter.InvokeAsync
    │  calls next(context) to execute the handler
    │  registers Response.OnStarting callback
    │
    ▼
Handler executes
    │  sets AuditContext properties if needed (ResourceId, SubResourceId, TargetUserId)
    │  returns IResult
    │
    ▼
Response.OnStarting fires (just before headers are sent)
    │  reads Response.StatusCode — final HTTP status is set by this point
    │  maps status code to Outcome string
    │  calls IAuditWriter.Enqueue(entry) — non-blocking TryWrite
    │
    ▼
HTTP Response sent to client

                        ┌─────────────────────────────────────┐
                        │  ChannelAuditWriter (background)    │
                        │  drains Channel<AuditEntry>         │
                        │  batches up to 50 / 5-second window │
                        │  bulk INSERT into AuditLog table    │
                        └─────────────────────────────────────┘
```

The `OnAuthenticationFailed` JWT event short-circuits this flow — it fires before routing reaches the endpoint pipeline, so `AuditEndpointFilter` is never called. The JWT event writes the audit entry directly to `IAuditWriter`.

### File Structure

```
Core/
  AuditLog/
    AuditEntry.cs               ← immutable DTO written to the DB
    AuditContext.cs             ← scoped per-request bag populated by handlers
    IAuditWriter.cs             ← interface: void Enqueue(AuditEntry)
    ChannelAuditWriter.cs       ← singleton background writer (IHostedService)
    AuditEndpointFilter.cs      ← IEndpointFilter; captures outcome via Response.OnStarting
    AuditLogSchemaInitializer.cs← DDL for the AuditLog table (idempotent IF NOT EXISTS)
  Program.cs                    ← registers services; wires OnAuthenticationFailed

Core.Tests/
  AuditLog/
    AuditLog.Http.Tests.cs      ← integration tests for all 7 outcome values
    ChannelAuditWriter.Tests.cs ← unit tests for drain loop, batching, and error handling
    CapturingAuditWriter.cs     ← test double that records entries in-memory
    NoOpAuditWriter.cs          ← test double that discards all entries
  ApiFactory.cs                 ← replaces IAuditWriter with NoOpAuditWriter by default
  TestDatabase.cs               ← SQLite schema including AuditLog table
```

---

## Database Schema

The `AuditLog` table is owned by the `AuditLog` feature slice and created by `AuditLogSchemaInitializer`. Creation is idempotent (`IF NOT EXISTS`).

```sql
CREATE TABLE AuditLog (
    Id              UNIQUEIDENTIFIER  NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Timestamp       DATETIMEOFFSET    NOT NULL,
    TraceId         NVARCHAR(32)      NULL,
    UserId          UNIQUEIDENTIFIER  NULL,
    IpAddress       NVARCHAR(45)      NULL,
    ResourceType    NVARCHAR(20)      NULL,
    Operation       NVARCHAR(50)      NOT NULL,
    ResourceId      UNIQUEIDENTIFIER  NULL,
    SubResourceId   UNIQUEIDENTIFIER  NULL,
    TargetUserId    UNIQUEIDENTIFIER  NULL,
    Outcome         NVARCHAR(20)      NOT NULL,
    FailureReason   NVARCHAR(500)     NULL
);
```

### Column Semantics

| Column | Description |
|---|---|
| `Id` | Sequential GUID primary key (`NEWSEQUENTIALID()`). Sequential insertion order improves clustered index performance for high-volume append workloads. |
| `Timestamp` | UTC timestamp of the event (`DateTimeOffset.UtcNow` at the point the `Response.OnStarting` callback fires). |
| `TraceId` | OpenTelemetry W3C trace ID (`Activity.Current?.TraceId`) for correlating audit records with distributed traces and application logs. NULL when no active trace exists. |
| `UserId` | GUID of the authenticated user performing the operation. NULL for unauthenticated requests (`AuthenticationFailed` events). |
| `IpAddress` | Client IP address. Resolved from `HttpContext.Connection.RemoteIpAddress` after `UseForwardedHeaders` middleware has applied `X-Forwarded-For`. NULL when not available. |
| `ResourceType` | Discriminator for the kind of resource acted upon — see values below. NULL for `AuthenticationFailed` events where no resource is involved. |
| `Operation` | Name of the operation. Matches the `.WithName()` value set on the endpoint, ensuring this is a stable compile-time identifier (e.g. `"CreateItemGroup"`). The special value `"AuthenticationFailed"` is used for JWT failures. |
| `ResourceId` | Primary resource GUID. For all Checklist operations this is `itemGroupId`. NULL for `GetItemGroups` and auth events. |
| `SubResourceId` | Secondary resource GUID. Populated only for Item operations where both `itemGroupId` and `itemId` are relevant. NULL for all other operations. |
| `TargetUserId` | GUID of the user being affected by the operation. Populated only for `AddMember` and `RemoveMember`. NULL for all other operations. |
| `Outcome` | Result string — see values below. |
| `FailureReason` | Human-readable reason for failure. Populated for `AuthenticationFailed` (the JWT exception message) and `MissingClaim` (the fixed string `"Required user identifier claim is missing."`). NULL for all other outcomes. |

### `ResourceType` Values

| Value | Used for |
|---|---|
| `'ItemGroup'` | All ItemGroup and Member operations |
| `'Item'` | All Item operations |
| NULL | `AuthenticationFailed` — no resource involved |

### `Outcome` Values

| Value | HTTP status | Meaning |
|---|---|---|
| `Success` | 200, 201, 204 | Operation completed successfully |
| `BadRequest` | 400 | Validation failed (e.g. blank name) |
| `MissingClaim` | 401 | JWT was valid but the `sub`/`NameIdentifier` claim is absent |
| `Forbidden` | 403 | Authenticated user is not a member of the target group |
| `NotFound` | 404 | Resource does not exist |
| `Conflict` | 409 | Duplicate member or attempt to remove the last member of a group |
| `AuthenticationFailed` | 401 | Bearer token was presented but rejected by JWT middleware |
| `Unknown` | other | Unrecognised status code — should not occur in normal operation |

`Unauthorized` is not used as an `Outcome` value. HTTP 401 maps to either `MissingClaim` (valid JWT, missing claim) or `AuthenticationFailed` (invalid JWT), which are meaningfully distinct for security analysis.

### Indexes

All indexes are composite with `Timestamp DESC` as the trailing key to support time-ordered queries filtered by a specific dimension without a full scan.

| Index | Purpose |
|---|---|
| `IX_AuditLog_UserId_Timestamp` | All activity for a specific user |
| `IX_AuditLog_ResourceId_Timestamp` | All events on a specific resource |
| `IX_AuditLog_ResourceType_Timestamp` | All events of a given resource type |
| `IX_AuditLog_Outcome_Timestamp` | All failures, all forbidden probes, etc. |
| `IX_AuditLog_Timestamp` | Time window queries with no other filter |

### Retention

Audit rows are stored indefinitely. Deletion is a manual administrative action only.

---

## Per-Operation Audit Data

### Checklist — ItemGroup

| Operation | `ResourceType` | `ResourceId` | `SubResourceId` | `TargetUserId` |
|---|---|---|---|---|
| `GetItemGroups` | `'ItemGroup'` | NULL | NULL | NULL |
| `GetItemGroup` | `'ItemGroup'` | `itemGroupId` | NULL | NULL |
| `CreateItemGroup` | `'ItemGroup'` | `itemGroupId` (set by handler after insert) | NULL | NULL |
| `UpdateItemGroup` | `'ItemGroup'` | `itemGroupId` | NULL | NULL |
| `DeleteItemGroup` | `'ItemGroup'` | `itemGroupId` | NULL | NULL |

### Checklist — Item

| Operation | `ResourceType` | `ResourceId` | `SubResourceId` | `TargetUserId` |
|---|---|---|---|---|
| `CreateItem` | `'Item'` | `itemGroupId` | `itemId` (set by handler after insert) | NULL |
| `UpdateItem` | `'Item'` | `itemGroupId` | `itemId` | NULL |
| `DeleteItem` | `'Item'` | `itemGroupId` | `itemId` | NULL |

### Checklist — Member

| Operation | `ResourceType` | `ResourceId` | `SubResourceId` | `TargetUserId` |
|---|---|---|---|---|
| `GetMembers` | `'ItemGroup'` | `itemGroupId` | NULL | NULL |
| `AddMember` | `'ItemGroup'` | `itemGroupId` | NULL | `memberId` |
| `RemoveMember` | `'ItemGroup'` | `itemGroupId` | NULL | `memberId` |

### Authentication

| Operation | `ResourceType` | `UserId` | `ResourceId` | Extra fields |
|---|---|---|---|---|
| `AuthenticationFailed` | NULL | NULL | NULL | `IpAddress`, `TraceId`, `FailureReason` (JWT exception message) |

---

## Implementation

### `AuditEntry`

`AuditEntry` is an immutable positional record — a plain DTO that represents one row in the `AuditLog` table. It lives in `Core/AuditLog/AuditEntry.cs` and is `internal` to the `Core` project except where test infrastructure needs it.

```csharp
public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string? TraceId,
    Guid? UserId,
    string? IpAddress,
    string? ResourceType,
    string Operation,
    Guid? ResourceId,
    Guid? SubResourceId,
    Guid? TargetUserId,
    string Outcome,
    string? FailureReason
);
```

`AuditEntry` is constructed in two places:
- `AuditEndpointFilter.EnqueueAuditEntry` — for all Checklist endpoint requests
- The `OnAuthenticationFailed` JWT event in `Program.cs` — for token validation failures

### `IAuditWriter` and `ChannelAuditWriter`

```csharp
public interface IAuditWriter
{
    void Enqueue(AuditEntry entry);
}
```

`IAuditWriter` is the only dependency that audit-producing code takes. All call sites call `Enqueue` and return immediately — there is no `async` path in the hot request path.

`ChannelAuditWriter` is the production implementation:

- **Channel**: `BoundedChannel<AuditEntry>` with capacity 10,000 and `DropOldest` full mode. If the database is unavailable for an extended period, old entries are silently discarded rather than allowing the channel to grow unbounded. `SingleReader = true`, `SingleWriter = false`.
- **Enqueue**: calls `channel.Writer.TryWrite(entry)` — synchronous, non-blocking. Returns without throwing even if the channel is full (the oldest entry is dropped instead).
- **Background drain** (`IHostedService`): A single long-running `Task` reads from the channel with a 5-second timeout window. It flushes when it accumulates 50 entries **or** when the 5-second window elapses — whichever comes first. This batches SQL writes to reduce database round-trips.
- **SQL write**: Opens a fresh `SqlConnection` using `IConfiguration.GetConnectionString("database")`. This connection is independent of the scoped `IDbConnection` used by request handlers, ensuring audit writes are never part of a handler transaction and cannot be rolled back by handler failures.
- **Error handling**: All exceptions from `WriteBatchAsync` are caught and logged at `Warning` level. The drain loop continues running. Lost entries are not retried.
- **Shutdown**: `StopAsync` marks the channel writer complete and cancels the drain task. The `finally` block in `DrainAsync` performs a final flush of any remaining buffered entries.

**Service registration** in `Program.cs`:

```csharp
builder.Services.AddSingleton<ChannelAuditWriter>();
builder.Services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<ChannelAuditWriter>());
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IHostedService, ChannelAuditWriter>(
        sp => sp.GetRequiredService<ChannelAuditWriter>())
);
```

Three registrations ensure one shared instance is reachable as `ChannelAuditWriter`, `IAuditWriter`, and `IHostedService`. Tests can replace `IAuditWriter` without touching the other two registrations.

### `AuditEndpointFilter`

`AuditEndpointFilter` is registered once on the entire Checklist route group:

```csharp
app.MapGroup("/api/list")
   .RequireAuthorization()
   .AddEndpointFilter<AuditEndpointFilter>();
```

Every request that reaches a Checklist endpoint — whether it succeeds or is rejected by the handler — produces exactly one audit entry.

**Why `Response.OnStarting` instead of inspecting the `IResult` return value**

Minimal API handlers declared as `Results<T1, T2, ...>` return a struct wrapper that does *not* implement `IStatusCodeHttpResult`. Pattern matching on the union type at filter level would require knowing all possible type combinations for every endpoint. Instead, the filter registers a `Response.OnStarting` callback immediately after calling `next(context)`. By the time this callback fires, ASP.NET Core has executed the `IResult` and set `Response.StatusCode` to the final HTTP status — including `403 Forbidden` set by `ForbidHttpResult` via the authorization pipeline. The callback reads `Response.StatusCode` as an integer and maps it to an `Outcome` string via a switch expression.

**Outcome mapping:**

```csharp
int statusCode => statusCode switch
{
    >= 200 and <= 299 => "Success",
    400               => "BadRequest",
    401               => "MissingClaim",
    403               => "Forbidden",
    404               => "NotFound",
    409               => "Conflict",
    _                 => "Unknown",
};
```

**Context captured before the callback fires:**

All values except `Response.StatusCode` are captured from the `HttpContext` before `Response.OnStarting` is registered, because some values (e.g. `RouteValues`, `AuditContext`) may not be accessible after response streaming begins:

- `operation` — from `IEndpointNameMetadata`
- `resourceType` — from the static lookup dictionary keyed on `operation`
- `resourceId` — from `AuditContext.ResourceId` if set, else from the `itemGroupId` route value
- `subResourceId` — from `AuditContext.SubResourceId` if set, else from the `itemId` route value
- `userId` — from `httpContext.User.GetUserId()` (reads `ClaimTypes.NameIdentifier`)
- `ipAddress` — from `httpContext.Connection.RemoteIpAddress`
- `traceId` — from `Activity.Current?.TraceId`
- `targetUserId` — from `auditContext.TargetUserId`

The entire operation (context extraction + callback registration) is wrapped in `try/catch` with a `Warning` log on failure. A faulty audit path must never surface as an unhandled exception to the client.

### `AuditContext`

`AuditContext` is a scoped DI service that handlers use to pass information to `AuditEndpointFilter` that cannot be derived from route values alone. It is registered as `services.AddScoped<AuditContext>()` and injected into handler method parameters — ASP.NET Core's Minimal API parameter binding resolves it from DI automatically.

```csharp
public sealed class AuditContext
{
    public Guid? ResourceId { get; set; }
    public Guid? SubResourceId { get; set; }
    public Guid? TargetUserId { get; set; }
}
```

**When `AuditContext` is needed:**

| Handler | Property set | Why |
|---|---|---|
| `CreateItemGroup` | `ResourceId` | The new group ID is generated inside the handler (`Guid.NewGuid()`). There is no `itemGroupId` route value on `POST /api/list`. |
| `CreateItem` | `SubResourceId` | The new item ID is generated inside the handler. The `itemGroupId` is a route value but the item's ID is not. |
| `AddMember` | `TargetUserId` | The `memberId` route value identifies the user being added. |
| `RemoveMember` | `TargetUserId` | Same as `AddMember`. |

**Handlers that do not take `AuditContext`** — `GetItemGroups`, `GetItemGroup`, `GetMembers`, `UpdateItemGroup`, `UpdateItem`, `DeleteItemGroup`, `DeleteItem`. The filter resolves all needed context for these handlers from route values or HTTP context without handler cooperation.

**Important**: `AuditContext.ResourceId` and `AuditContext.SubResourceId` are only set on the *successful* code path inside `CreateItemGroup` and `CreateItem` respectively. For early-exit paths (BadRequest, Unauthorized, Forbidden) the handler returns before setting these properties, so the filter records `NULL`. This is correct — when a create operation was not permitted, no resource was created.

### JWT `OnAuthenticationFailed` Event

`AuditEndpointFilter` only runs for requests that reach the endpoint pipeline. A request bearing an invalid Bearer token is rejected by the JWT middleware before routing — the endpoint pipeline (including the filter) is never reached. The `OnAuthenticationFailed` event in the `AddJwtBearer` configuration is the only interception point for these events.

```csharp
options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = context =>
    {
        IAuditWriter writer =
            context.HttpContext.RequestServices.GetRequiredService<IAuditWriter>();
        writer.Enqueue(new AuditEntry(
            Timestamp: DateTimeOffset.UtcNow,
            TraceId: Activity.Current?.TraceId.ToString(),
            UserId: null,
            IpAddress: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            ResourceType: null,
            Operation: "AuthenticationFailed",
            ResourceId: null,
            SubResourceId: null,
            TargetUserId: null,
            Outcome: "AuthenticationFailed",
            FailureReason: context.Exception.Message
        ));
        return Task.CompletedTask;
    },
};
```

`OnChallenge` is intentionally **not** handled. It fires for all unauthenticated requests — including health checks, `OPTIONS` preflights, and requests with no token at all — producing excessive noise with no useful security signal.

### Schema Initializer

`AuditLogSchemaInitializer.CreateSchemaAsync` creates the `AuditLog` table and all indexes inside an `IF NOT EXISTS` guard, making it safe to run on every application startup.

`DatabaseInitializer` orchestrates schema creation in sequence:

```csharp
await ChecklistSchemaInitializer.CreateSchemaAsync(connection, ct);
await AuditLogSchemaInitializer.CreateSchemaAsync(connection, ct);
```

Schema initialization is skipped entirely in the `Testing` environment (controlled by `Program.cs`). Tests create their own SQLite in-memory schema via `TestDatabase.CreateTablesAsync`.

### Middleware Order

The middleware pipeline in `Program.cs` must follow this order:

```
app.UseForwardedHeaders(...)   // resolves X-Forwarded-For into RemoteIpAddress — MUST be first
app.UseCors(...)
app.UseHttpsRedirection()      // skipped in Testing environment
app.UseAuthentication()        // JWT validation; OnAuthenticationFailed fires here
app.UseAuthorization()
app.MapChecklistApi()          // registers route group with AuditEndpointFilter
```

`UseForwardedHeaders` must precede `UseAuthentication` so that `HttpContext.Connection.RemoteIpAddress` is set to the real client IP before the JWT event or the audit filter reads it.

---

## Adding Audit Logging to a New Endpoint

Endpoints on the `/api/list` route group automatically receive an audit entry because `AuditEndpointFilter` is applied to the entire group. The following steps are all that is required.

**1. Register the endpoint name with `.WithName()`**

```csharp
builder
    .MapGet("/{itemGroupId:guid}/something", Execute)
    .WithName(nameof(GetSomething));   // ← must match the handler class name
```

The filter uses this name as the `Operation` value. Without it, the filter logs a warning and exits without writing an entry.

**2. Add the operation to the `resourceTypes` dictionary in `AuditEndpointFilter`**

```csharp
private static readonly Dictionary<string, string> resourceTypes = new(StringComparer.Ordinal)
{
    // existing entries ...
    [nameof(GetSomething)] = "ItemGroup",   // ← add here
};
```

**3. Inject `AuditContext` only if the handler generates IDs or affects another user**

If the handler generates a resource ID internally (not from route values) or affects a target user, add `AuditContext auditContext` to the handler parameters and set the relevant property before returning the success result:

```csharp
public static async Task<Results<Created<Thing>, ...>> Execute(
    Guid itemGroupId,
    // ...
    AuditContext auditContext,
    CancellationToken ct = default)
{
    // ...
    Thing thing = await CreateData(...);
    auditContext.SubResourceId = thing.Id;   // only if ID is generated here
    return TypedResults.Created(...);
}
```

For handlers where all context (IDs, user) is already in route values, `AuditContext` is not required.

---

## Test Infrastructure

### `NoOpAuditWriter`

`NoOpAuditWriter` is the default `IAuditWriter` in `ApiFactory`. It discards all entries, making audit behaviour invisible to tests that do not care about it.

```csharp
internal sealed class NoOpAuditWriter : IAuditWriter
{
    public void Enqueue(AuditEntry entry) { }
}
```

`ApiFactory` swaps out only `IAuditWriter`. `ChannelAuditWriter` itself and its `IHostedService` registration are left in place — the hosted service starts and drains normally, but since `IAuditWriter` resolves to `NoOpAuditWriter`, entries are never enqueued and no SQL connections are attempted.

### `CapturingAuditWriter`

For tests that assert on audit behaviour, create a `CapturingAuditWriter` and replace `IAuditWriter` for that specific test using `WithWebHostBuilder`:

```csharp
var writer = new CapturingAuditWriter();
await using WebApplicationFactory<Program> webFactory = factory.WithWebHostBuilder(b =>
    b.ConfigureServices(services =>
    {
        services.RemoveAll<IAuditWriter>();
        services.AddSingleton<IAuditWriter>(writer);
    })
);
HttpClient client = webFactory.CreateClient();

// Act
await client.PostAsJsonAsync("/api/list", new { Name = "My Group" });

// Assert
AuditEntry entry = Assert.Single(writer.Entries);
Assert.Equal("CreateItemGroup", entry.Operation);
Assert.Equal("Success", entry.Outcome);
Assert.NotNull(entry.ResourceId);
```

```csharp
internal sealed class CapturingAuditWriter : IAuditWriter
{
    public List<AuditEntry> Entries { get; } = [];
    public void Enqueue(AuditEntry entry) => Entries.Add(entry);
}
```

`CapturingAuditWriter` is instantiated *outside* the DI container and passed as an instance. The test holds a direct reference to `Entries` without needing to resolve anything from the container.

**Testing `AuthenticationFailed`** requires switching the default authentication scheme back to `"Bearer"` so the JWT handler processes the token instead of the test scheme:

```csharp
services.PostConfigure<AuthenticationOptions>(opts =>
{
    opts.DefaultAuthenticateScheme = "Bearer";
    opts.DefaultChallengeScheme = "Bearer";
});
```

Then send a request with a malformed Bearer token. `OnAuthenticationFailed` fires, the entry is written to `CapturingAuditWriter`, and `AuditEndpointFilter` never runs (the request is blocked before routing).

### `TestDatabase`

`TestDatabase.CreateTablesAsync` creates the SQLite-compatible schema for integration tests. The `AuditLog` table uses `TEXT` columns in place of `UNIQUEIDENTIFIER` and `DATETIMEOFFSET`. A custom Dapper `GuidTypeHandler` maps `Guid` ↔ `TEXT` for all queries.

`CreateTablesAsync` is `internal` so that tests can call it directly on a freshly opened connection with non-default settings (e.g. `Foreign Keys=False` for the `NotFound` outcome test that needs to insert an orphaned `Members` row).

---

## Future Extensibility

### Field-Level Change Tracking

When needed, add a nullable `Changes NVARCHAR(MAX)` column to store a JSON diff of the before/after values for update operations:

- Only `UpdateItem` and `UpdateItemGroup` need changes: perform an extra SELECT before the update, compute the diff, and store it on `AuditContext` (a new property)
- No changes to the filter, `ChannelAuditWriter`, schema initializer conditional guard, or any other handler
- Existing rows have NULL in the new column — no backfill required

### Admin UI

The `AuditLog` table is designed for direct SQL query access. Common patterns:

```sql
-- All activity for a specific user
SELECT * FROM AuditLog WHERE UserId = @userId ORDER BY Timestamp DESC;

-- All events on a specific resource
SELECT * FROM AuditLog WHERE ResourceId = @id ORDER BY Timestamp DESC;

-- Authentication failures (both JWT invalid and missing claim)
SELECT * FROM AuditLog
WHERE Outcome IN ('AuthenticationFailed', 'MissingClaim')
ORDER BY Timestamp DESC;

-- Forbidden probes from a specific IP (potential reconnaissance)
SELECT * FROM AuditLog
WHERE Outcome = 'Forbidden' AND IpAddress = @ip
ORDER BY Timestamp DESC;

-- All activity within a time window
SELECT * FROM AuditLog
WHERE Timestamp BETWEEN @from AND @to
ORDER BY Timestamp DESC;
```

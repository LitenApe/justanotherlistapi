# Audit Log Specification

## Table of Contents

- [Overview](#overview)
- [Database Schema](#database-schema)
  - [Column Semantics](#column-semantics)
  - [ResourceType Values](#resourcetype-values)
  - [Outcome Values](#outcome-values)
  - [Retention](#retention)
- [Per-Operation Audit Data](#per-operation-audit-data)
  - [Checklist — ItemGroup](#checklist--itemgroup)
  - [Checklist — Item](#checklist--item)
  - [Checklist — Member](#checklist--member)
  - [Authentication](#authentication)
- [Implementation Components](#implementation-components)
  - [1. DatabaseInitializer Refactor](#1-databaseinitializer-refactor)
  - [2. AuditEntry Record](#2-auditentry-record)
  - [3. AuditContext — Scoped Service](#3-auditcontext--scoped-service)
  - [4. IAuditWriter and ChannelAuditWriter](#4-iauditwriter-and-channelauditwriter)
  - [5. AuditEndpointFilter](#5-auditendpointfilter)
  - [6. JWT OnAuthenticationFailed Event](#6-jwt-onauthenticationfailed-event)
  - [7. ForwardedHeaders Middleware](#7-forwardedheaders-middleware)
  - [8. Test Infrastructure](#8-test-infrastructure)
- [Future Extensibility](#future-extensibility)
  - [Field-Level Change Tracking](#field-level-change-tracking)
  - [User Feature](#user-feature)
  - [Admin UI](#admin-ui)

---

## Overview

Audit logging tracks all attempted and successful operations across the API, including authentication failures, to support security monitoring, attack detection, and administrative review. A future Admin UI will expose the audit log for querying and management.

Audit logging must not affect the availability or correctness of normal API functionality. Write failures must be silently absorbed. The performance impact on request handling must be minimised.

---

## Database Schema

The `AuditLog` table is owned by the `AuditLog` feature slice and created by `AuditLogSchemaInitializer`.

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
    ResourceName    NVARCHAR(500)     NULL,
    Outcome         NVARCHAR(20)      NOT NULL,
    FailureReason   NVARCHAR(500)     NULL
);

CREATE INDEX IX_AuditLog_UserId_Timestamp        ON AuditLog (UserId,       Timestamp DESC);
CREATE INDEX IX_AuditLog_ResourceId_Timestamp    ON AuditLog (ResourceId,   Timestamp DESC);
CREATE INDEX IX_AuditLog_ResourceType_Timestamp  ON AuditLog (ResourceType, Timestamp DESC);
CREATE INDEX IX_AuditLog_Outcome_Timestamp       ON AuditLog (Outcome,      Timestamp DESC);
CREATE INDEX IX_AuditLog_Timestamp               ON AuditLog (Timestamp     DESC);
```

### Column Semantics

| Column | Description |
|---|---|
| `Id` | Sequential GUID for clustered index performance on high-insert tables |
| `Timestamp` | UTC timestamp of the event |
| `TraceId` | OpenTelemetry trace ID for correlation with distributed traces |
| `UserId` | The authenticated user performing the operation. NULL when unauthenticated |
| `IpAddress` | Real client IP after `ForwardedHeaders` middleware resolves `X-Forwarded-For` |
| `ResourceType` | Discriminator for the resource being acted upon — see values below |
| `Operation` | Name of the operation — matches `.WithName()` on the endpoint, or `AuthenticationFailed` |
| `ResourceId` | Primary resource identifier (always `itemGroupId` for Checklist operations) |
| `SubResourceId` | Secondary resource identifier (`itemId` for Item operations) |
| `TargetUserId` | The user being affected (populated for `AddMember` and `RemoveMember`) |
| `ResourceName` | Name of the resource at the time of the operation — see population rules below |
| `Outcome` | Result of the operation — see values below |
| `FailureReason` | Human-readable reason for failure. Populated for `AuthenticationFailed` and `MissingClaim` |

### `ResourceType` Values

| Value | Used for |
|---|---|
| `'ItemGroup'` | All ItemGroup and Member operations |
| `'Item'` | All Item operations |
| `'User'` | Future User profile operations |
| NULL | `AuthenticationFailed` — no resource involved |

### `Outcome` Values

| Value | Meaning |
|---|---|
| `Success` | 200, 201, or 204 response |
| `BadRequest` | Validation failed (e.g. blank name) |
| `NotFound` | Resource does not exist |
| `Conflict` | 409 — duplicate member or last-member removal attempt |
| `Forbidden` | Authenticated user is not a member of the target group |
| `MissingClaim` | JWT was valid and accepted, but `sub`/`user_id` claim is absent |
| `AuthenticationFailed` | Token was presented but rejected by JWT middleware |

`Unauthorized` (HTTP 401) is not used as an outcome value. The two distinct auth failure modes are represented by `MissingClaim` and `AuthenticationFailed`.

### Retention

Audit rows are stored indefinitely. Deletion is a manual administrative action only.

---

## Per-Operation Audit Data

### Checklist — ItemGroup

| Operation | `ResourceType` | `ResourceId` | `SubResourceId` | `TargetUserId` | `ResourceName` |
|---|---|---|---|---|---|
| `GetItemGroups` | `'ItemGroup'` | NULL | NULL | NULL | NULL |
| `GetItemGroup` | `'ItemGroup'` | `itemGroupId` | NULL | NULL | NULL |
| `CreateItemGroup` | `'ItemGroup'` | `itemGroupId` (after insert) | NULL | NULL | NULL |
| `UpdateItemGroup` | `'ItemGroup'` | `itemGroupId` | NULL | NULL | New name from request body |
| `DeleteItemGroup` | `'ItemGroup'` | `itemGroupId` | NULL | NULL | Name fetched via SELECT before delete |

### Checklist — Item

| Operation | `ResourceType` | `ResourceId` | `SubResourceId` | `TargetUserId` | `ResourceName` |
|---|---|---|---|---|---|
| `CreateItem` | `'Item'` | `itemGroupId` | `itemId` (after insert) | NULL | NULL |
| `UpdateItem` | `'Item'` | `itemGroupId` | `itemId` | NULL | New name from request body |
| `DeleteItem` | `'Item'` | `itemGroupId` | `itemId` | NULL | Name fetched via SELECT before delete |

### Checklist — Member

| Operation | `ResourceType` | `ResourceId` | `SubResourceId` | `TargetUserId` | `ResourceName` |
|---|---|---|---|---|---|
| `GetMembers` | `'ItemGroup'` | `itemGroupId` | NULL | NULL | NULL |
| `AddMember` | `'ItemGroup'` | `itemGroupId` | NULL | `memberId` | NULL |
| `RemoveMember` | `'ItemGroup'` | `itemGroupId` | NULL | `memberId` | NULL |

### Authentication

| Operation | `ResourceType` | Fields populated |
|---|---|---|
| `AuthenticationFailed` | NULL | `IpAddress`, `TraceId`, `FailureReason` (exception message from JWT middleware) |

---

## Implementation Components

### 1. `DatabaseInitializer` Refactor

`DatabaseInitializer` becomes a thin orchestrator. Each feature slice owns its own schema initializer.

```
Core/
  DatabaseInitializer.cs           ← calls each slice initializer in sequence
  Checklist/
    ChecklistSchemaInitializer.cs  ← existing ItemGroups, Items, Members DDL moved here
  AuditLog/
    AuditLogSchemaInitializer.cs   ← new AuditLog DDL
```

`DatabaseInitializer.InitializeAsync` opens the connection then calls:
1. `ChecklistSchemaInitializer.CreateSchemaAsync`
2. `AuditLogSchemaInitializer.CreateSchemaAsync`

Order matters only if cross-feature foreign keys exist. Currently none do.

---

### 2. `AuditEntry` Record

A plain DTO. Lives in `Core/AuditLog/`.

```csharp
internal sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string? TraceId,
    Guid? UserId,
    string? IpAddress,
    string? ResourceType,
    string Operation,
    Guid? ResourceId,
    Guid? SubResourceId,
    Guid? TargetUserId,
    string? ResourceName,
    string Outcome,
    string? FailureReason
);
```

---

### 3. `AuditContext` — Scoped Service

Allows individual handlers to pass semantic context (name, target user) to the filter without the filter needing to re-parse the request body.

```csharp
public sealed class AuditContext
{
    public Guid? ResourceId { get; set; }
    public Guid? SubResourceId { get; set; }
    public string? ResourceName { get; set; }
    public Guid? TargetUserId { get; set; }
}
```

Registered as `services.AddScoped<AuditContext>()`. Injected into handlers that need it as an additional parameter — Minimal API binds it from DI automatically.

`ResourceId` and `SubResourceId` are only needed when the IDs are not available as route values (i.e. they are generated inside the handler). The filter checks `AuditContext.ResourceId` first and falls back to the `itemGroupId` route value; it checks `AuditContext.SubResourceId` first and falls back to the `itemId` route value. Handlers that do not set these properties are unaffected.

Handlers that populate `AuditContext`:

| Handler | Sets |
|---|---|
| `CreateItemGroup` | `ResourceId` = the newly generated `itemGroupId` (set after a successful insert, before returning `Created`) |
| `CreateItem` | `SubResourceId` = the newly generated `itemId` (set after a successful insert, before returning `Created`) |
| `UpdateItemGroup` | `ResourceName` = new name from request body |
| `UpdateItem` | `ResourceName` = new name from request body |
| `DeleteItemGroup` | `ResourceName` = fetched from DB before delete |
| `DeleteItem` | `ResourceName` = fetched from DB before delete |
| `AddMember` | `TargetUserId` = `memberId` route value |
| `RemoveMember` | `TargetUserId` = `memberId` route value |

All other handlers do not take `AuditContext`.

#### Why `CreateItemGroup` and `CreateItem` need `AuditContext` for IDs

`CreateItemGroup` posts to `/` with no route parameters — `AuditContext.ResourceId` is the only path for the filter to obtain the new group ID. `CreateItem` posts to `/{itemGroupId:guid}` so `itemGroupId` is present in route values, but the new item's ID is generated inside the handler (`Guid.NewGuid()`) and is not a route value. `AuditContext.SubResourceId` is populated by the handler after a successful insert so the filter can record it. Only successful (201 Created) responses produce a meaningful ID; for early-exit responses (BadRequest, Unauthorized, Forbidden) the handler returns before setting `AuditContext.SubResourceId`, so the filter records `NULL` for those outcomes.

---

### 4. `IAuditWriter` and `ChannelAuditWriter`

```csharp
public interface IAuditWriter
{
    void Enqueue(AuditEntry entry);
}
```

`ChannelAuditWriter` is a singleton that:
- Holds an unbounded `Channel<AuditEntry>` (or bounded with drop-oldest policy to prevent unbounded memory growth under sustained load)
- Exposes `Enqueue` as a non-blocking `TryWrite` — no await, no blocking
- Implements `IHostedService` to drain the channel in the background
- Batches writes: flush when 50 entries are queued OR after 5 seconds, whichever comes first
- Opens its own `SqlConnection` using `IConfiguration.GetConnectionString("database")` — this is the standard .NET connection string key that Aspire populates at startup with the provisioned SQL Server connection string, so `ChannelAuditWriter` automatically connects to the same SQL Server instance that Aspire provisions. It **never reuses the scoped `IDbConnection`** from the request pipeline, ensuring audit writes are independent of any handler transactions.
- Catches all exceptions during write, logs at `Warning` level via `ILogger`, and continues draining

Registered in `Program.cs` using three separate descriptors so that tests can replace only the parts they need without risking cast failures:

```csharp
// Concrete singleton — shared instance for both IAuditWriter and IHostedService
builder.Services.AddSingleton<ChannelAuditWriter>();
// IAuditWriter alias pointing to the same instance
builder.Services.AddSingleton<IAuditWriter>(sp => sp.GetRequiredService<ChannelAuditWriter>());
// IHostedService entry — uses ServiceDescriptor directly so ImplementationType is set,
// allowing tests to find and remove this entry precisely by ImplementationType
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IHostedService, ChannelAuditWriter>(
        sp => sp.GetRequiredService<ChannelAuditWriter>()));
```

Registering all three separately ensures:
1. `IAuditWriter` and `IHostedService` resolve to the same `ChannelAuditWriter` instance.
2. Tests can replace `IAuditWriter` without triggering a `ChannelAuditWriter` cast at host startup.
3. Tests can remove the `IHostedService` entry by `ImplementationType == typeof(ChannelAuditWriter)` without affecting any other hosted services.

---

### 5. `AuditEndpointFilter`

Registered once on the Checklist route group in `ChecklistApiEndpointRouteBuilderExtension`:

```csharp
group.AddEndpointFilter<AuditEndpointFilter>();
```

Responsibilities:
1. Call `next(context)` to execute the handler
2. Read route values from `HttpContext.Request.RouteValues`:
   - `ResourceId`: use `AuditContext.ResourceId` if set (populated by `CreateItemGroup` after insert); otherwise fall back to the `itemGroupId` route value
   - `SubResourceId`: use `AuditContext.SubResourceId` if set (populated by `CreateItem` after insert); otherwise fall back to the `itemId` route value (present on `UpdateItem` and `DeleteItem`)
3. Read `UserId` via `httpContext.User.GetUserId()`
4. Read `IpAddress` from `HttpContext.Connection.RemoteIpAddress` (corrected by ForwardedHeaders middleware)
5. Read `Operation` from `EndpointNameMetadata` (already set via `.WithName()` on every endpoint)
6. Read `ResourceType` from a static lookup dictionary keyed on `Operation`
7. Read `AuditContext` from `HttpContext.RequestServices`
8. Map the returned `IResult` type to an `Outcome` string via pattern matching on `TypedResults` types
9. Call `_auditWriter.Enqueue(entry)` — non-blocking
10. Wrap steps 1–9 in try/catch; log any exception at `Warning` and continue

The `Outcome` mapping:

| `IResult` type | `Outcome` |
|---|---|
| `Created<T>`, `Ok<T>`, `NoContent` | `Success` |
| `NotFound` | `NotFound` |
| `BadRequest` | `BadRequest` |
| `Conflict` | `Conflict` |
| `ForbidHttpResult` | `Forbidden` |
| `UnauthorizedHttpResult` | `MissingClaim` |

---

### 6. JWT `OnAuthenticationFailed` Event

In the `AddJwtBearer` configuration block in `Program.cs`:

```csharp
options.Events = new JwtBearerEvents
{
    OnAuthenticationFailed = async context =>
    {
        var writer = context.HttpContext.RequestServices.GetRequiredService<IAuditWriter>();
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
            ResourceName: null,
            Outcome: "AuthenticationFailed",
            FailureReason: context.Exception.Message
        ));
    }
};
```

`OnChallenge` is intentionally **not** handled — it fires for all unauthenticated requests including health checks and browser preflights, producing excessive noise with no useful signal.

---

### 7. `ForwardedHeaders` Middleware

Must be registered **first** in the middleware pipeline in `Program.cs`, before authentication, so that `HttpContext.Connection.RemoteIpAddress` reflects the real client IP when read by the filter and the JWT event.

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

Configure `KnownProxies` or `KnownNetworks` appropriately for the production deployment environment to avoid IP spoofing via crafted `X-Forwarded-For` headers.

---

### 8. Test Infrastructure

#### `NoOpAuditWriter`

Registered in `ApiFactory.ConfigureServices` to prevent the real `ChannelAuditWriter` from attempting SQL Server connections during tests. Because `ChannelAuditWriter` is registered with three separate descriptors, all three must be cleaned up:

```csharp
// Remove the ChannelAuditWriter concrete singleton and the IAuditWriter alias
services.RemoveAll<ChannelAuditWriter>();
services.RemoveAll<IAuditWriter>();

// Remove the IHostedService entry for ChannelAuditWriter.
// ServiceDescriptor.Singleton<IHostedService, ChannelAuditWriter>(factory) sets
// ImplementationType even on a factory descriptor, so this lookup is reliable.
var toRemove = services
    .Where(d => d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(ChannelAuditWriter))
    .ToList();
foreach (var sd in toRemove) services.Remove(sd);

// Register the no-op replacement
services.AddSingleton<IAuditWriter, NoOpAuditWriter>();
```

```csharp
internal sealed class NoOpAuditWriter : IAuditWriter
{
    public void Enqueue(AuditEntry entry) { }
}
```

`ApiFactory` registers `NoOpAuditWriter` by default so that audit behaviour is invisible in tests that do not care about it.

#### `CapturingAuditWriter`

For tests that assert on audit behaviour, create a `CapturingAuditWriter` instance in the test and swap it in using `WithWebHostBuilder`:

```csharp
internal sealed class CapturingAuditWriter : IAuditWriter
{
    public List<AuditEntry> Entries { get; } = [];
    public void Enqueue(AuditEntry entry) => Entries.Add(entry);
}
```

Usage pattern in a test:

```csharp
var capturingWriter = new CapturingAuditWriter();

using var client = factory
    .WithWebHostBuilder(b => b.ConfigureServices(services =>
    {
        // ApiFactory has already removed ChannelAuditWriter and its hosted service.
        // Only the IAuditWriter (NoOpAuditWriter) replacement needs to be swapped here.
        services.RemoveAll<IAuditWriter>();
        services.AddSingleton<IAuditWriter>(capturingWriter);
    }))
    .CreateClient();

// Act — make HTTP calls via client

// Assert
Assert.Single(capturingWriter.Entries);
Assert.Equal("CreateItemGroup", capturingWriter.Entries[0].Operation);
Assert.Equal("Success", capturingWriter.Entries[0].Outcome);
```

Because `capturingWriter` is instantiated outside the DI container and passed in as a singleton instance, the test has direct access to `Entries` without needing to resolve anything from the container.

#### `TestDatabase`

Add the `AuditLog` table to `CreateTablesAsync` in SQLite-compatible DDL (no `NEWSEQUENTIALID()`, TEXT instead of UNIQUEIDENTIFIER):

```sql
CREATE TABLE AuditLog (
    Id            TEXT NOT NULL PRIMARY KEY,
    Timestamp     TEXT NOT NULL,
    TraceId       TEXT NULL,
    UserId        TEXT NULL,
    IpAddress     TEXT NULL,
    ResourceType  TEXT NULL,
    Operation     TEXT NOT NULL,
    ResourceId    TEXT NULL,
    SubResourceId TEXT NULL,
    TargetUserId  TEXT NULL,
    ResourceName  TEXT NULL,
    Outcome       TEXT NOT NULL,
    FailureReason TEXT NULL
);
```

---

## Future Extensibility

### Field-Level Change Tracking

Deferred to a future version. When needed:
- Add a nullable `Changes NVARCHAR(MAX)` column containing a JSON diff (e.g. `{"IsComplete": {"from": true, "to": false}}`)
- Only `UpdateItem` and `UpdateItemGroup` handlers need changes: one extra SELECT before the update, compute diff, set on `AuditContext`
- No changes to the filter, writer, or other handlers
- Existing rows will have NULL in the new column — no backfill needed

### User Feature

When the `User` feature slice is added:
- `UserSchemaInitializer.CreateSchemaAsync` is added to `DatabaseInitializer`
- User profile handlers use `ResourceType = 'User'`, `ResourceId = null` or the user's own ID as appropriate
- No schema changes to `AuditLog` required

### Admin UI

The `AuditLog` table is designed for direct query access. Recommended Admin UI queries:
- All events by user: `WHERE UserId = @userId ORDER BY Timestamp DESC`
- All events on a resource: `WHERE ResourceId = @id ORDER BY Timestamp DESC`
- All auth failures: `WHERE Outcome IN ('AuthenticationFailed', 'MissingClaim') ORDER BY Timestamp DESC`
- All forbidden probes by IP: `WHERE Outcome = 'Forbidden' AND IpAddress = @ip ORDER BY Timestamp DESC`
- Activity within a time window: `WHERE Timestamp BETWEEN @from AND @to ORDER BY Timestamp DESC`

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
| `CreateItem` | `'Item'` | `itemGroupId` | NULL | NULL | NULL |
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
    public string? ResourceName { get; set; }
    public Guid? TargetUserId { get; set; }
}
```

Registered as `services.AddScoped<AuditContext>()`. Injected into handlers that need it as an additional parameter — Minimal API binds it from DI automatically.

Handlers that populate `AuditContext`:

| Handler | Sets |
|---|---|
| `UpdateItemGroup` | `ResourceName` = new name from request body |
| `UpdateItem` | `ResourceName` = new name from request body |
| `DeleteItemGroup` | `ResourceName` = fetched from DB before delete |
| `DeleteItem` | `ResourceName` = fetched from DB before delete |
| `AddMember` | `TargetUserId` = `memberId` route value |
| `RemoveMember` | `TargetUserId` = `memberId` route value |

All other handlers do not take `AuditContext`.

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
- Opens its own `SqlConnection` using the raw connection string from `IConfiguration["ConnectionStrings:database"]` — **never reuses the scoped `IDbConnection`** from the request pipeline, ensuring audit writes are independent of any handler transactions
- Catches all exceptions during write, logs at `Warning` level via `ILogger`, and continues draining

Registered in `Program.cs`:
```csharp
services.AddSingleton<IAuditWriter, ChannelAuditWriter>();
services.AddHostedService(sp => (ChannelAuditWriter)sp.GetRequiredService<IAuditWriter>());
```

---

### 5. `AuditEndpointFilter`

Registered once on the Checklist route group in `ChecklistApiEndpointRouteBuilderExtension`:

```csharp
group.AddEndpointFilter<AuditEndpointFilter>();
```

Responsibilities:
1. Call `next(context)` to execute the handler
2. Read route values (`itemGroupId`, `itemId`) from `HttpContext.Request.RouteValues`
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

Registered in `ApiFactory.ConfigureServices` to prevent the real `ChannelAuditWriter` from attempting SQL Server connections during tests:

```csharp
services.AddSingleton<IAuditWriter, NoOpAuditWriter>();
```

```csharp
internal sealed class NoOpAuditWriter : IAuditWriter
{
    public void Enqueue(AuditEntry entry) { }
}
```

#### `CapturingAuditWriter`

For tests that assert on audit behavior, inject a `CapturingAuditWriter`:

```csharp
internal sealed class CapturingAuditWriter : IAuditWriter
{
    public List<AuditEntry> Entries { get; } = [];
    public void Enqueue(AuditEntry entry) => Entries.Add(entry);
}
```

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

# JustAnotherList

JustAnotherList is an open-source, minimalist app for organising life into shareable checklists. Create unlimited lists, invite collaborators, and track items — all through a clean REST API.

## Contents

- [Tech stack](#tech-stack)
- [Project structure](#project-structure)
- [Requirements](#requirements)
- [Quickstart (local development)](#quickstart-local-development)
- [Getting a token locally](#getting-a-token-locally)
- [Running without Aspire](#running-without-aspire-alternative-setup)
- [Configuration reference](#configuration-reference)
- [Authentication](#authentication)
- [Database schema](#database-schema)
- [API endpoints](#api-endpoints)
- [Tests](#tests)
- [Code quality](#code-quality)
- [Contributing](#contributing)

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| Backend | ASP.NET Core 10 (Minimal API) |
| Data access | Dapper + SQL Server |
| Authentication | JWT Bearer — delegated to an external OAuth 2.0 / OIDC provider |
| API docs | Scalar (OpenAPI) |
| Observability | OpenTelemetry (traces + logs, OTLP export) |
| Dev orchestration | .NET Aspire |

## Project structure

```
Aspire/                             - .NET Aspire AppHost — wires all services for local development
  Program.cs                        - Registers SQL Server, mock OAuth container, and Core project
Core/                               - ASP.NET Core backend
  Program.cs                        - App composition root: DI, middleware, auth, OpenAPI, DB init
  DatabaseInitializer.cs            - Runs idempotent CREATE TABLE + CREATE INDEX at startup
  Checklist/
    ChecklistApiEndpointRouteBuilderExtension.cs  - Maps all routes under /api/list
    ChecklistConnectionExtensions.cs              - IDbConnection.IsMember() shared helper
    Item/                           - CreateItem, UpdateItem, DeleteItem handlers
    ItemGroup/                      - GetItemGroups, GetItemGroup, CreateItemGroup,
    |                                 UpdateItemGroup, DeleteItemGroup handlers
    Member/                         - GetMembers, AddMember, RemoveMember handlers
  Extensions/
    ClaimsPrincipalExtension.cs     - GetUserId(): reads sub / NameIdentifier / user_id claim → Guid?
  Utility/
    BearerSecuritySchemeTransformer.cs  - Adds Bearer + OAuth2 security schemes to OpenAPI doc
Core.Tests/                         - xUnit unit tests (SQLite in-memory, no running server needed)
  TestDatabase.cs                   - Creates in-memory SQLite with matching schema + GuidTypeHandler
  TestHelpers.cs                    - CreatePrincipal(Guid): builds a ClaimsPrincipal for tests
  Checklist/                        - Handler tests mirroring the Core/Checklist structure
Directory.Build.props               - Solution-wide MSBuild settings and analyzer packages
Directory.Build.targets             - CSharpier format check wired into every CLI build
dotnet-tools.json                   - Pins CSharpier version for local tool restore
```

## Requirements

- .NET SDK 10
- Docker (or another OCI-compatible runtime)

Docker is required because Aspire spins up SQL Server and the OAuth mock server as persistent containers at startup.

## Quickstart (local development)

1. Clone the repo and restore local tools:

   ```bash
   git clone <repo-url>
   cd justanotherlistapi
   dotnet tool restore
   ```

2. Start the Aspire host:

   ```bash
   dotnet run --project Aspire
   ```

   Aspire starts and health-checks the following containers before launching the backend:

   | Container | Image | Port | Lifetime |
   |---|---|---|---|
   | SQL Server | `mcr.microsoft.com/mssql/server` (via Aspire) | auto-assigned | Persistent |
   | mock-oauth2-server | `ghcr.io/navikt/mock-oauth2-server:2.1.10` | `8080` | Persistent |

   Both containers are **persistent** — data and the OAuth server survive restarts. The backend waits for both to be healthy before starting. Database tables are created automatically on first boot.

3. Open the Aspire dashboard URL printed in the console. From there you can navigate to the running `Core` service and open the Scalar UI at `/scalar/v1`.

## Getting a token locally

When the app is running with Aspire, the Scalar UI at `/scalar/v1` is pre-configured with `client_id: 00000000-0000-0000-0000-000000000001` and `client_secret: dev`. Click **Authorize → Get Token** in Scalar to fetch a token directly from the mock server — no copy-pasting required.

Alternatively, fetch a token manually:

```bash
curl -X POST http://localhost:8080/default/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=00000000-0000-0000-0000-000000000001&client_secret=dev"
```

Copy the returned `access_token` and supply it as a `Bearer` token in any HTTP client. The `sub` claim will be used as the caller's user ID — it must be a valid `Guid` (the mock server generates one by default).

## Running without Aspire (alternative setup)

If you prefer a local SQL Server install or an external endpoint, configure the connection string and OAuth authority directly in `Core/appsettings.Development.json` (this file is gitignored):

```json
{
  "ConnectionStrings": {
    "database": "Server=localhost,1433;Database=JustAnotherListDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
  },
  "OAuth": {
    "Authority": "https://your-provider.com/tenant"
  }
}
```

Then run the backend directly:

```bash
dotnet run --project Core
```

The database tables are created automatically on first startup.

## Configuration reference

All runtime configuration for `Core` is resolved from `appsettings.json`, environment variables, and Aspire-injected values.

| Key | Required | Description |
|---|---|---|
| `ConnectionStrings:database` | Yes | SQL Server connection string. Injected by Aspire; set manually when running standalone. |
| `OAuth:Authority` | Yes | OIDC issuer URL (e.g. `https://login.microsoftonline.com/{tenant}/v2.0`). Passed to `AddJwtBearer`. Aspire injects this as `OAuth__Authority`. |
| `OAuth:Audience` | No | Expected JWT audience. If omitted, audience validation is disabled. |

> **Note:** `launchSettings.json` files are gitignored. Each developer's local launch profiles are not committed.

## Authentication

The API validates JWT Bearer tokens issued by an external OAuth 2.0 / OIDC provider.

- `RequireHttpsMetadata` is automatically disabled when `OAuth:Authority` starts with `http://` (supports the local mock server).
- If `OAuth:Audience` is not set, audience validation is skipped.

### User identity

Every protected operation resolves the caller's identity via `ClaimsPrincipal.GetUserId()`, which checks claims in this priority order:

1. `ClaimTypes.NameIdentifier`
2. `sub`
3. `user_id`

The value must parse as a valid `Guid`; otherwise `null` is returned and the request gets a `401 Unauthorized`.

### Authorization model

- All `/api/list` routes require a valid Bearer token (`RequireAuthorization`).
- Every route that operates on a specific item group also checks that the caller is a member of that group via a `SELECT COUNT(1) FROM Members` query. Non-members receive `403 Forbidden`.

## Database schema

The schema is managed without a migration framework. `DatabaseInitializer` runs idempotent SQL on every startup — safe against an existing database, no CLI tooling required. To change the schema, edit `Core/DatabaseInitializer.cs` directly.

### Tables

```sql
CREATE TABLE ItemGroups (
    Id   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name NVARCHAR(MAX)    NOT NULL
);

CREATE TABLE Items (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name        NVARCHAR(MAX)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    IsComplete  BIT              NOT NULL DEFAULT 0,
    ItemGroupId UNIQUEIDENTIFIER NOT NULL,
    FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
);

CREATE TABLE Members (
    MemberId    UNIQUEIDENTIFIER NOT NULL,
    ItemGroupId UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY (MemberId, ItemGroupId),
    FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
);
```

Deleting an `ItemGroup` cascades and removes all related `Items` and `Members` rows.

### Indexes

```sql
CREATE INDEX IX_Items_ItemGroupId            ON Items(ItemGroupId);
CREATE INDEX IX_Members_ItemGroupId          ON Members(ItemGroupId);
CREATE INDEX IX_Members_MemberId_ItemGroupId ON Members(MemberId, ItemGroupId);
```

All indexes and tables guard against re-creation with `NOT EXISTS` checks against `sys.tables` / `sys.indexes`.

## API endpoints

All endpoints are under `/api/list` and require a valid Bearer token. Interactive docs are available at `/scalar/v1`.

### Item groups

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/list` | List all item groups the caller belongs to |
| `POST` | `/api/list` | Create a new item group (caller is auto-added as first member) |
| `GET` | `/api/list/{id}` | Get a single item group with all items and members |
| `PUT` | `/api/list/{id}` | Rename an item group |
| `DELETE` | `/api/list/{id}` | Delete an item group (cascades to items and members) |

### Items

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/list/{groupId}` | Create an item inside a group |
| `PUT` | `/api/list/{groupId}/{itemId}` | Update an item |
| `DELETE` | `/api/list/{groupId}/{itemId}` | Delete an item |

### Members

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/list/{id}/member` | List member IDs of a group |
| `POST` | `/api/list/{id}/member/{memberId}` | Add a member to a group |
| `DELETE` | `/api/list/{id}/member/{memberId}` | Remove a member from a group |

### Request / response shapes

#### `GET /api/list` → `200 OK`

Returns only groups the caller is a member of. The `members` array is always empty in this response. The `items` array contains **only incomplete** items.

```json
[
  {
    "id": "3fa85f64-...",
    "name": "Shopping",
    "items": [
      { "id": "...", "name": "Milk", "description": null, "isComplete": false, "itemGroupId": "..." }
    ],
    "members": []
  }
]
```

#### `GET /api/list/{id}` → `200 OK` / `401` / `403`

Returns the group with **all** items (complete and incomplete) and **all** member IDs.

```json
{
  "id": "3fa85f64-...",
  "name": "Shopping",
  "items": [ { "id": "...", "name": "Milk", "description": null, "isComplete": false, "itemGroupId": "..." } ],
  "members": [ "a1b2c3d4-..." ]
}
```

#### `POST /api/list` → `201 Created` / `400` / `401`

```json
{ "name": "Shopping" }
```

Returns the created group. The caller is automatically added as the first member.

#### `PUT /api/list/{id}` → `204 No Content` / `400` / `401` / `403`

```json
{ "name": "New name" }
```

`name` must be non-empty and non-whitespace; otherwise `400`.

#### `POST /api/list/{groupId}` — Create item → `201 Created` / `400` / `401` / `403`

```json
{ "name": "Milk", "description": "2% fat", "isComplete": false }
```

`name` is required. `description` is optional (`null` allowed). `isComplete` defaults to `false`.

#### `PUT /api/list/{groupId}/{itemId}` → `204 No Content` / `400` / `401` / `403`

Same body shape as create item. All fields are required; the entire item is replaced.

#### `POST /api/list/{id}/member/{memberId}` → `204 No Content` / `401` / `403` / `409 Conflict`

No request body. Returns `409 Conflict` if `memberId` is already a member.

#### `DELETE /api/list/{id}/member/{memberId}` → `204 No Content` / `401` / `403`

Idempotent — no error if `memberId` is not a member.

### Common status codes

| Status | Meaning |
|---|---|
| `401 Unauthorized` | Missing or invalid Bearer token, or `sub` claim is not a valid `Guid` |
| `403 Forbidden` | Caller is authenticated but is not a member of the requested group |
| `404 Not Found` | Group does not exist (only `GET /api/list/{id}`) |
| `409 Conflict` | Target is already a member (`POST …/member/{memberId}`) |

## Tests

Tests use xUnit with an in-memory SQLite database — no running SQL Server or OAuth server needed.

```bash
dotnet test
```

### How tests work

- `Core.Tests` references `Core` internals via `InternalsVisibleTo`, so individual handler `Execute()` methods are called directly — no HTTP server is spun up.
- `TestDatabase` creates an in-memory SQLite connection and runs the same schema (with SQLite-compatible types: `TEXT` instead of `UNIQUEIDENTIFIER`, `INTEGER` instead of `BIT`). A `GuidTypeHandler` is registered with Dapper so `Guid` ↔ `TEXT` mapping works transparently.
- `TestHelpers.CreatePrincipal(Guid)` builds a `ClaimsPrincipal` with a `ClaimTypes.NameIdentifier` claim for the given user ID.

### Test coverage by file

| Test file | What it covers |
|---|---|
| `ClaimsPrincipalExtension.Tests.cs` | `GetUserId()` — all claim types, invalid GUIDs, empty values |
| `CreateItemGroup.Tests.cs` | Happy path, DB persistence, auto-member insert, validation, auth |
| `GetItemGroups.Tests.cs` | Multi-group listing, only-incomplete items, empty result, auth |
| `GetItemGroup.Tests.cs` | Full group with all items + members, membership gate, auth |
| `UpdateItemGroup.Tests.cs` | Name update, validation, membership gate, auth |
| `DeleteItemGroup.Tests.cs` | Deletion, cascade behaviour, membership gate, auth |
| `CreateItem.Tests.cs` | Create with all fields, validation, membership gate, auth |
| `UpdateItem.Tests.cs` | Full replace of all fields, validation, membership gate, auth |
| `DeleteItem.Tests.cs` | Deletion scoped by `ItemGroupId`, cross-group safety, auth |
| `AddMember.Tests.cs` | Add new member, conflict on duplicate, membership gate, auth |
| `GetMembers.Tests.cs` | List member IDs, membership gate, auth |
| `RemoveMember.Tests.cs` | Remove member (idempotent), membership gate, auth |

## Code quality

Formatting and linting are enforced automatically on every `dotnet build` run from the CLI.

| Tool | Purpose |
|---|---|
| [CSharpier](https://csharpier.com) | Opinionated code formatter (like Prettier for C#) |
| [.editorconfig](.editorconfig) | Formatting and naming conventions for the IDE and `dotnet format` |
| .NET SDK analyzers (`AnalysisLevel=latest-recommended`) | Built-in code quality rules |
| [Roslynator](https://github.com/dotnet/roslynator) | 500+ additional code quality and refactoring rules |
| [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) | ~140 rules for async patterns, culture safety, string handling, and more |

All warnings are treated as errors (`TreatWarningsAsErrors=true` in `Directory.Build.props`) — the build fails if any rule is violated. This includes `.editorconfig` naming rules such as `PascalCase` for types and `camelCase` for locals.

The CSharpier check is wired into `Directory.Build.targets` as a `BeforeTargets="Build"` target. It is automatically skipped for IDE incremental builds (`BuildingInsideVisualStudio=true`) and design-time builds (`DesignTimeBuild=true`).

### Useful commands

```bash
# Format all code in place (run before committing)
dotnet csharpier format .

# Check formatting without writing changes (what the build runs)
dotnet csharpier check .

# Auto-fix Roslyn style violations (explicit types, braces, etc.)
dotnet format

# Skip the CSharpier check for a single build (e.g. mid-refactor)
dotnet build -p:CSharpierDisable=true
```

After cloning, restore the local tools to get the pinned CSharpier version:

```bash
dotnet tool restore
```

## Contributing

- Author / maintainer: Son Thanh Vo
- Contributions and forks are welcome. Open issues or PRs for changes.


# JustAnotherList

JustAnotherList is an open-source, minimalist app for organising life into shareable checklists. Create unlimited lists, invite collaborators, and track items — through a clean REST API or the accessible, responsive web client.

## Contents

- [Tech stack](#tech-stack)
- [Project structure](#project-structure)
- [Specifications](#specifications)
- [Requirements](#requirements)
- [Quickstart (local development)](#quickstart-local-development)
- [Using the web client](#using-the-web-client)
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

| Layer             | Technology                                                        |
| ----------------- | ----------------------------------------------------------------- |
| Runtime           | .NET 10                                                           |
| Backend           | ASP.NET Core 10 (Minimal API)                                     |
| Data access       | Dapper + SQL Server                                               |
| Authentication    | JWT Bearer — delegated to an external OAuth 2.0 / OIDC provider   |
| API docs          | Scalar (OpenAPI)                                                  |
| Observability     | OpenTelemetry (traces + logs, OTLP export)                        |
| Dev orchestration | .NET Aspire                                                       |
| Frontend          | React 19 + TypeScript, React Router framework mode (SPA), SignalR |
| Frontend quality  | ESLint (typescript-eslint, jsx-a11y), Prettier, Vitest, axe-core  |

## Project structure

```
Aspire/                             - .NET Aspire AppHost — wires all services for local development
  Program.cs                        - Registers SQL Server, mock OAuth container, Core project and Vite client
Client/                             - React Router (framework mode, SPA) web client — see specifications/client.md
  src/root.tsx, src/routes.ts       - HTML document + app shell, and the route config
  src/api/                          - fetch wrapper and one function per API endpoint
  src/auth/                         - Session store, dev sign-in against the mock OAuth server
  src/realtime/                     - Single SignalR connection with per-list subscriptions
  src/components/                   - Layout, dialogs, toasts/live regions, icons
  src/lists/                        - "Your lists" overview page
  src/list/                         - List page: items, filters, members, dialogs
  src/theme/                        - Light/dark theme (dark by default) with a header toggle
  src/styles/global.css             - Design tokens per theme (WCAG AA contrast) and base styles
  src/test/                         - Fake API, fake SignalR connection, axe helper
Core/                               - ASP.NET Core backend
  Program.cs                        - App composition root: DI, middleware, auth, OpenAPI, DB init
  DatabaseInitializer.cs            - Runs idempotent CREATE TABLE + CREATE INDEX at startup
  AuditLog/
    AuditContext.cs                 - Scoped per-request bag handlers use to pass resource IDs to the filter
    AuditEndpointFilter.cs          - IEndpointFilter that enqueues an AuditEntry after every request
    AuditEntry.cs                   - Immutable record representing a single audited event
    AuditLogSchemaInitializer.cs    - Idempotent DDL for the AuditLog table and its indexes
    ChannelAuditWriter.cs           - IAuditWriter + IHostedService: batches entries via a bounded Channel
    IAuditWriter.cs                 - Enqueue(AuditEntry) abstraction consumed by handlers and the filter
  Checklist/
    ChecklistApiEndpointRouteBuilderExtension.cs  - Maps all routes under /api/list
    ChecklistAuthorizationExtensions.cs           - ExecuteAsItemGroupMember/ExecuteAsAuthenticatedUser helpers
    ChecklistCleanupService.cs      - BackgroundService that runs AutoDeleteItemGroups once a minute
    ChecklistConnectionExtensions.cs              - IDbConnection.IsMember() and IsLastMember() shared helpers
    ChecklistHub.cs                 - SignalR hub at /hubs/checklist — JoinGroup (membership-gated) and LeaveGroup
    ChecklistNotifier.cs            - IChecklistNotifier impl: broadcasts via IHubContext with optional caller exclusion
    IChecklistClient.cs             - Typed client interface (ItemCreated, ItemUpdated, etc.)
    IChecklistNotifier.cs           - Abstraction for broadcasting notifications (testable)
    Item/                           - CreateItem, UpdateItem, DeleteItem handlers
    ItemGroup/                      - GetItemGroups, GetItemGroup, CreateItemGroup, UpdateItemGroup,
    |                                 UpdateItemGroupSettings, DeleteItemGroup handlers, AutoDeleteItemGroups
    Member/                         - GetMembers, AddMember, RemoveMember handlers
  Extensions/
    ClaimsPrincipalExtension.cs     - GetUserId(): reads sub / NameIdentifier / user_id claim → Guid?
  Utility/
    BearerSecuritySchemeTransformer.cs  - Adds Bearer + OAuth2 security schemes to OpenAPI doc
Core.Tests/                         - xUnit tests (SQL Server via Testcontainers, requires Docker)
  ApiFactory.cs                     - WebApplicationFactory<Program> on a test DB + TestAuthHandler (user picked per request)
  TestDatabase.cs                   - Starts shared SQL Server container; isolated DB per test
  TestHelpers.cs                    - CreatePrincipal(Guid): builds a ClaimsPrincipal for unit tests
  AuditLog/
    AuditLog.Http.Tests.cs          - HTTP integration tests verifying audit entries are captured per operation
    CapturingAuditWriter.cs         - In-memory IAuditWriter that accumulates entries for assertion
    ChannelAuditWriter.Tests.cs     - Unit tests for ChannelAuditWriter (batching, flush, failure handling)
    NoOpAuditWriter.cs              - IAuditWriter that discards all entries (used to isolate other tests)
  Checklist/
    CapturingNotifier.cs            - In-memory IChecklistNotifier that records notifications for assertion
    ChecklistHubTests.cs            - SignalR hub auth tests + full end-to-end notification pipeline tests
    Item/                           - Unit + HTTP tests for Item endpoints
    ItemGroup/                      - Unit + HTTP tests for ItemGroup endpoints
    Member/                         - Unit + HTTP tests for Member endpoints
Directory.Build.props               - Solution-wide MSBuild settings and analyzer packages
Directory.Build.targets             - CSharpier format check wired into every CLI build
dotnet-tools.json                   - Pins CSharpier version for local tool restore
global.json                         - Opts `dotnet test` into Microsoft.Testing.Platform
coverage.config                     - Code coverage settings used by CI
specifications/                     - Design and architecture specifications
  checklist.md                      - Checklist feature: domain model, endpoints, DB schema
  audit-logs.md                     - Audit log feature: schema, implementation design
  authentication.md                 - Authentication: JWT setup, claim resolution
  client.md                         - Web client: structure, sign-in, realtime, accessibility
```

## Specifications

Design and architecture documents for planned and implemented features live in the [`specifications/`](specifications/) folder.

| Document                                              | Description                                                                                                                                        |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| [checklist.md](specifications/checklist.md)           | Checklist feature — domain model, authorization model, database schema, all API endpoints with request/response shapes, and structural conventions |
| [audit-logs.md](specifications/audit-logs.md)         | Audit Log feature — database schema, per-operation audit data, implementation design, and future extensibility                                     |
| [authentication.md](specifications/authentication.md) | Authentication feature — JWT Bearer setup, token validation behavior, user identity claim resolution, and how users authenticate against the API   |
| [client.md](specifications/client.md)                 | Web client — structure, dev sign-in, data flow, real-time updates, accessibility (WCAG 2.2 AA) and responsive design conventions                   |

## Requirements

- .NET SDK 10
- Docker (or another OCI-compatible runtime)
- Node.js 24 (for the web client; Aspire runs `npm install` and the React Router dev server)

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

   | Container          | Image                                        | Port          | Lifetime   |
   | ------------------ | -------------------------------------------- | ------------- | ---------- |
   | SQL Server         | `mcr.microsoft.com/mssql/server:2025-latest` | auto-assigned | Persistent |
   | mock-oauth2-server | `ghcr.io/navikt/mock-oauth2-server:6.1.1`    | `8080`        | Persistent |

   Both containers are **persistent** — data and the OAuth server survive restarts. The backend waits for both to be healthy before starting. Database tables are created automatically on first boot.

3. Open the web client at **http://localhost:5173** (see [Using the web client](#using-the-web-client)), or open the Aspire dashboard URL printed in the console to reach the `Core` service and its Scalar UI at `/scalar/v1`.

## Using the web client

The client signs in against the local mock OAuth server. Your **user ID** (a GUID) is your identity: the sign-in page prefills `00000000-0000-0000-0000-000000000001`, and **Generate a new user ID** creates another one.

- Each browser tab keeps its own sign-in, so you can open a second tab with a different user ID to try sharing.
- To share a list, ask the other person for their user ID (shown under **Account**) and paste it under **Add a member** on the list page.
- Changes made by other members appear live.
- **Settings** on the list page can hide done items after a while and delete the list once every item has been done for a while (at least a day). Both are off by default and apply to every member.
- Dark mode is the default; use the **Dark mode** toggle in the header to switch to light. Your choice is remembered in the browser.

The client is built to WCAG 2.2 AA (keyboard and screen reader support, visible focus, sufficient contrast in light and dark mode) and works from 320px phone screens up to desktop. Details are in [specifications/client.md](specifications/client.md).

To work on the client without Aspire, start Core and the OAuth server, then run `npm install` and `npm run dev` in `Client/`.

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

| Key                          | Required | Description                                                                                                                                    |
| ---------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:database` | Yes      | SQL Server connection string. Injected by Aspire; set manually when running standalone.                                                        |
| `OAuth:Authority`            | Yes      | OIDC issuer URL (e.g. `https://login.microsoftonline.com/{tenant}/v2.0`). Passed to `AddJwtBearer`. Aspire injects this as `OAuth__Authority`. |
| `OAuth:Audience`             | No       | Expected JWT audience. If omitted, audience validation is disabled.                                                                            |

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

Four tables exist: `ItemGroups`, `Items`, `Members`, and `AuditLog`. Deleting an `ItemGroup` cascades and removes all related `Items` and `Members` rows. The `AuditLog` table is managed by `AuditLogSchemaInitializer` and records every API operation with its outcome, resource identifiers, and OpenTelemetry trace ID.

For the full DDL, column definitions, and indexes see [specifications/checklist.md](specifications/checklist.md#database-schema).

## API endpoints

All endpoints are under `/api/list` and require a valid Bearer token. Interactive docs are available at `/scalar/v1`.

### Item groups

| Method   | Route            | Description                                                    |
| -------- | ---------------- | -------------------------------------------------------------- |
| `GET`    | `/api/list`      | List all item groups the caller belongs to                     |
| `POST`   | `/api/list`      | Create a new item group (caller is auto-added as first member) |
| `GET`    | `/api/list/{id}` | Get a single item group with all items and members             |
| `PUT`    | `/api/list/{id}` | Rename an item group                                           |
| `DELETE` | `/api/list/{id}` | Delete an item group (cascades to items and members)           |

### Items

| Method   | Route                          | Description                   |
| -------- | ------------------------------ | ----------------------------- |
| `POST`   | `/api/list/{groupId}`          | Create an item inside a group |
| `PUT`    | `/api/list/{groupId}/{itemId}` | Update an item                |
| `DELETE` | `/api/list/{groupId}/{itemId}` | Delete an item                |

### Members

| Method   | Route                              | Description                                                                            |
| -------- | ---------------------------------- | -------------------------------------------------------------------------------------- |
| `GET`    | `/api/list/{id}/member`            | List member IDs of a group                                                             |
| `POST`   | `/api/list/{id}/member/{memberId}` | Add a member to a group                                                                |
| `DELETE` | `/api/list/{id}/member/{memberId}` | Remove a member from a group. Returns `409 Conflict` if the target is the last member. |

For detailed request/response shapes, validation rules, and edge cases see [specifications/checklist.md](specifications/checklist.md#api-endpoints).

### Real-time notifications (SignalR)

A SignalR hub at `/hubs/checklist` pushes mutations to connected group members in real time.

| Hub method   | Description                                                                  |
| ------------ | ---------------------------------------------------------------------------- |
| `JoinGroup`  | Subscribe to notifications for a group (membership is validated server-side) |
| `LeaveGroup` | Unsubscribe from a group's notifications                                     |

**Client events** pushed by the server: `ItemCreated`, `ItemUpdated`, `ItemDeleted`, `MemberAdded`, `MemberRemoved`, `GroupRenamed`, `GroupDeleted`.

**Caller exclusion**: Clients send an `X-SignalR-Connection-Id` header on mutations, with the connection ID from the hub's negotiate response. The server uses `GroupExcept` to avoid echoing the event back to the originator.

The negotiate request authenticates with the usual `Authorization: Bearer` header; the WebSocket uses an `?access_token=` query string parameter (standard SignalR convention for WebSocket transport).

### Common status codes

| Status             | Meaning                                                                                                                           |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------- |
| `401 Unauthorized` | Missing or invalid Bearer token, or `sub` claim is not a valid `Guid`                                                             |
| `403 Forbidden`    | Caller is authenticated but is not a member of the requested group                                                                |
| `404 Not Found`    | Group does not exist (only `GET /api/list/{id}`)                                                                                  |
| `409 Conflict`     | Target is already a member (`POST …/member/{memberId}`), or target is the last member of the group (`DELETE …/member/{memberId}`) |

## Tests

Tests use xUnit against a real SQL Server started by [Testcontainers](https://dotnet.testcontainers.org/) — Docker must be running, but no manually started SQL Server or OAuth server is needed.

```bash
dotnet test
```

Client tests come in four kinds: **unit** tests of pure logic (Vitest in Node), **browser** tests of components in real Chromium (Vitest browser mode), **integration** tests of the production build in Playwright with only the network (REST, OAuth and the SignalR WebSocket) faked, and **end-to-end** tests of the whole application with nothing faked. The end-to-end setup starts SQL Server and the mock OAuth server in Docker, runs the real Core and serves the client build, and the tests check collaboration between real users through the backend and SignalR. Pages, dialogs and both themes are checked with axe-core, colour contrast included. See [specifications/client.md](specifications/client.md#testing).

```bash
cd Client
npx playwright install chromium   # once
npm run check      # typecheck + lint + format check + unit, browser and integration tests
npm run test:e2e   # end-to-end: real Core, SQL Server and OAuth server (needs Docker)
```

### How tests work

`Core.Tests` contains two kinds of tests:

**Unit tests** call handler `Execute()` methods directly against a SQL Server test database.

- `Core.Tests` references `Core` internals via `InternalsVisibleTo`, so individual handler `Execute()` methods are called directly — no HTTP server is spun up.
- `TestDatabase` lazily starts one `mcr.microsoft.com/mssql/server:2025-latest` container per test run, creates a uniquely named database for each `CreateAsync()` call, and applies the production schema via `ChecklistSchemaInitializer` and `AuditLogSchemaInitializer`.
- `TestHelpers.CreatePrincipal(Guid)` builds a `ClaimsPrincipal` with a `ClaimTypes.NameIdentifier` claim for the given user ID.

**HTTP integration tests** boot the full ASP.NET Core app via `WebApplicationFactory` and make real HTTP requests, covering route registration and middleware.

- `ApiFactory` points the app's own scoped `SqlConnection` at an isolated `TestDatabase` database, so requests open connections exactly as in production; `factory.Connection` is a separate open connection for arranging data and checking results.
- It swaps JWT Bearer authentication for a `TestAuthHandler` that signs requests in as a fixed user ID by default. `factory.CreateClientFor(userId)` acts as another user and `factory.CreateAnonymousClient()` as no one.
- It keeps the **real** `ChecklistNotifier` and SignalR hub, so tests can connect with `factory.CreateHubConnection()` and verify notifications are broadcast through the actual pipeline.
- The app runs in the `"Testing"` environment, which skips HTTPS redirection and database initialisation.

### Test coverage by file

| Test file                           | What it covers                                                                            |
| ----------------------------------- | ----------------------------------------------------------------------------------------- |
| `ApiFactory.Tests.cs`               | The app gets its own scoped, closed connection to the test database                       |
| `ClaimsPrincipalExtension.Tests.cs` | `GetUserId()` — all claim types, invalid GUIDs, empty values                              |
| `CreateItemGroup.Tests.cs`          | Happy path, DB persistence, auto-member insert, validation, auth                          |
| `CreateItemGroup.Http.Tests.cs`     | `POST /api/list` route registration                                                       |
| `GetItemGroups.Tests.cs`            | Multi-group listing, only-incomplete items, empty result, auth                            |
| `GetItemGroups.Http.Tests.cs`       | `GET /api/list` route registration, 401 when not signed in                                |
| `GetItemGroup.Tests.cs`             | Full group with all items + members, membership gate, auth                                |
| `GetItemGroup.Http.Tests.cs`        | `GET /api/list/{id}` route registration, 403 for non-members                              |
| `UpdateItemGroup.Tests.cs`          | Name update, validation, membership gate, auth, notification assertion                    |
| `UpdateItemGroup.Http.Tests.cs`     | `PUT /api/list/{id}` route registration                                                   |
| `DeleteItemGroup.Tests.cs`          | Deletion, cascade behaviour, membership gate, auth, notification assertion                |
| `DeleteItemGroup.Http.Tests.cs`     | `DELETE /api/list/{id}` route registration                                                |
| `CreateItem.Tests.cs`               | Create with all fields, validation, membership gate, auth, notification assertion         |
| `CreateItem.Http.Tests.cs`          | `POST /api/list/{groupId}` route registration                                             |
| `UpdateItem.Tests.cs`               | Full replace of all fields, validation, membership gate, auth, notification assertion     |
| `UpdateItem.Http.Tests.cs`          | `PUT /api/list/{groupId}/{itemId}` route registration                                     |
| `DeleteItem.Tests.cs`               | Deletion scoped by `ItemGroupId`, cross-group safety, auth, notification assertion        |
| `DeleteItem.Http.Tests.cs`          | `DELETE /api/list/{groupId}/{itemId}` route registration                                  |
| `AddMember.Tests.cs`                | Add new member, conflict on duplicate, membership gate, auth, notification assertion      |
| `AddMember.Http.Tests.cs`           | `POST /api/list/{id}/member/{memberId}` route registration                                |
| `GetMembers.Tests.cs`               | List member IDs, membership gate, auth                                                    |
| `GetMembers.Http.Tests.cs`          | `GET /api/list/{id}/member` route registration                                            |
| `RemoveMember.Tests.cs`             | Remove member (idempotent), last-member conflict, membership gate, auth, notification     |
| `RemoveMember.Http.Tests.cs`        | `DELETE /api/list/{id}/member/{memberId}` route registration                              |
| `ChecklistHubTests.cs`              | Hub auth (JoinGroup/LeaveGroup), full SignalR pipeline for all 7 events, caller exclusion |
| `AuditLog.Http.Tests.cs`            | Verifies an `AuditEntry` is captured for every operation (resource IDs, outcome, user ID) |
| `ChannelAuditWriter.Tests.cs`       | Batching (50-entry immediate flush, 5 s window flush), failure recovery, shutdown drain   |

## Code quality

Formatting and linting are enforced automatically on every `dotnet build` run from the CLI.

| Tool                                                                  | Purpose                                                                  |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| [CSharpier](https://csharpier.com)                                    | Opinionated code formatter (like Prettier for C#)                        |
| [.editorconfig](.editorconfig)                                        | Formatting and naming conventions for the IDE and `dotnet format`        |
| .NET SDK analyzers (`AnalysisLevel=latest-recommended`)               | Built-in code quality rules                                              |
| [Roslynator](https://github.com/dotnet/roslynator)                    | 500+ additional code quality and refactoring rules                       |
| [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) | ~140 rules for async patterns, culture safety, string handling, and more |

All warnings are treated as errors (`TreatWarningsAsErrors=true` in `Directory.Build.props`) — the build fails if any rule is violated. This includes `.editorconfig` naming rules such as `PascalCase` for types and `camelCase` for locals.

The client has its own checks, run by `npm run check` in `Client/` and in CI: TypeScript strict mode, ESLint with `typescript-eslint` (strict, type-aware), `eslint-plugin-jsx-a11y` (strict) and `eslint-plugin-react-hooks`, and Prettier formatting.

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

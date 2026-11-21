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
| Runtime           | .NET 11                                                           |
| Backend           | ASP.NET Core 11 (Minimal API)                                     |
| Data access       | Dapper + SQL Server                                               |
| Authentication    | Accounts with ASP.NET Core Identity; the API issues and checks its own JWTs |
| API docs          | Scalar (OpenAPI)                                                  |
| Observability     | OpenTelemetry (traces + logs, OTLP export)                        |
| Dev orchestration | .NET Aspire                                                       |
| Frontend          | React 19 + TypeScript, React Router framework mode (SPA), SignalR |
| Frontend quality  | ESLint (typescript-eslint, jsx-a11y), Prettier, Vitest, axe-core  |

## Project structure

```
Aspire/                             - .NET Aspire AppHost — wires all services for local development
  Program.cs                        - Registers SQL Server, the Api project and the Vite client
Client/                             - React Router (framework mode, SPA) web client — see specifications/client.md
  src/root.tsx, src/routes.ts       - HTML document + app shell, and the route config
  src/api/                          - fetch wrapper and one function per API endpoint
  src/auth/                         - Session store, sign-up and sign-in pages, the account rules
  src/realtime/                     - Single SignalR connection with per-list subscriptions
  src/components/                   - Layout, dialogs, toasts/live regions, icons
  src/lists/                        - "Your lists" overview page
  src/list/                         - List page: items, filters, members, dialogs
  src/theme/                        - Light/dark theme (dark by default) with a header toggle
  src/styles/global.css             - Design tokens per theme (WCAG AA contrast) and base styles
  src/test/                         - Fake API, fake SignalR connection, axe helper
Checklist/                          - The inside of the hexagon, no packages — see specifications/architecture.md
  Domain/                           - ItemGroup, ItemGroupSettings, Item, User records (also the JSON and SignalR payloads), Caller, Name, ItemInput, ItemGroupSettingsInput, DisplayName, EmailAddress, Password, Session
  Ports/                            - IMembership (who belongs to which list), IItemGroupStore (the lists), IItemStore (their items), IChecklistNotifier (realtime updates), IAuditTrail (audit entries without a request), IAccounts (accounts and passwords), ITokenIssuer (sign-in tokens), IMailer (emails)
  UseCases/Account/                 - SignUp, SignIn, RequestPasswordReset, ResetPassword
  UseCases/Item/                    - CreateItem, UpdateItem, DeleteItem
  UseCases/ItemGroup/               - GetItemGroups, GetItemGroup, CreateItemGroup, UpdateItemGroup, UpdateItemGroupSettings, DeleteItemGroup, AutoDeleteItemGroups
  UseCases/Member/                  - GetMembers, AddMember, RemoveMember, SubscribeToItemGroup (for the hub)
Api/                                - ASP.NET Core backend: the adapters around Checklist
  Program.cs                        - App composition root: DI, middleware, auth, OpenAPI, DB init
  ChecklistServiceCollectionExtensions.cs  - AddChecklist(): registers use cases, plugs adapters into ports
  Email/
    SmtpMailer.cs                   - IMailer over SMTP: the password reset email and its link
  Accounts/
    IdentityAccounts.cs             - IAccounts on ASP.NET Core Identity: hashes and checks passwords, locks out guessing
    SqlAccountStore.cs              - Identity's store on the Users table, with Dapper
    JwtTokenIssuer.cs               - ITokenIssuer: JWTs signed with the Auth:SigningKey secret
    AuthOptions.cs                  - The Auth configuration section: issuer, audience, key, token lifetime
  AuditLog/
    AuditContext.cs                 - Scoped per-request bag handlers use to pass resource IDs to the filter
    AuditEndpointFilter.cs          - IEndpointFilter that enqueues an AuditEntry after every request
    AuditEntry.cs                   - Immutable record representing a single audited event
    AuditLogSchemaInitializer.cs    - Idempotent DDL for the AuditLog table and its indexes
    AuditTrail.cs                   - IAuditTrail on the audit log: entries no request is behind (auto-delete)
    AuthenticationAudit.cs          - Audits rejected bearer tokens, which never reach the endpoint filter
    RateLimitAudit.cs               - Audits requests over the account endpoints' rate limit
    ChannelAuditWriter.cs           - IAuditWriter + IHostedService: batches entries via a bounded Channel
    IAuditWriter.cs                 - Enqueue(AuditEntry) abstraction consumed by handlers and the filter
  Background/
    ChecklistCleanupService.cs      - BackgroundService that runs the AutoDeleteItemGroups use case once a minute, in a scope of its own
  Extensions/
    ClaimsPrincipalExtension.cs     - GetUserId(): reads sub / NameIdentifier / user_id claim → Guid?
  Http/
    BearerSecuritySchemeTransformer.cs  - Adds the Bearer security scheme to the OpenAPI document
    AccountApiEndpointRouteBuilderExtension.cs  - MapAccountApi(): maps the account routes under /api/auth
    Account/                        - Endpoint adapters: SignUp, SignIn, RequestPasswordReset (forgot-password), ResetPassword
    AuthRateLimit.cs                - How often a client may call the account endpoints
    CallerExtensions.cs             - ToCaller(): the signed-in user and SignalR connection behind a request
    ChecklistApiEndpointRouteBuilderExtension.cs  - MapChecklistApi(): maps all routes under /api/list
    Item/                           - Endpoint adapters: CreateItem, UpdateItem, DeleteItem
    ItemGroup/                      - Endpoint adapters: GetItemGroups, GetItemGroup, CreateItemGroup, UpdateItemGroup, UpdateItemGroupSettings, DeleteItemGroup
    Member/                         - Endpoint adapters: GetMembers, AddMember, RemoveMember
  Realtime/
    ChecklistHub.cs                 - SignalR hub at /hubs/checklist — JoinGroup (membership-gated) and LeaveGroup
    ChecklistNotifier.cs            - IChecklistNotifier impl: broadcasts via IHubContext with optional caller exclusion
    IChecklistClient.cs             - Typed client interface (ItemCreated, ItemUpdated, etc.)
  Sql/
    ChecklistSchemaInitializer.cs   - Idempotent DDL for ItemGroups, Items and Members, upgrading older databases in place
    DatabaseInitializer.cs          - Runs both schema initializers at startup
    ItemGroupRow.cs                 - Flat ItemGroups row for Dapper, turned into an ItemGroup
    SqlItemGroupStore.cs            - IItemGroupStore on the ItemGroups table; settings and auto-delete in one statement each
    SqlItemStore.cs                 - IItemStore on the Items table; editing a completed item keeps its completion time
    SqlMembership.cs                - IMembership on the Members table, with race-safe add and last-member removal
Api.Tests/                          - xUnit tests (SQL Server via Testcontainers, requires Docker)
  ApiFactory.cs                     - WebApplicationFactory<Program> on a test DB + TestAuthHandler (user picked per request)
  Architecture.Tests.cs             - Dependency rules: Checklist uses only .NET, use cases take only ports, adapters only use cases
  CompositionRoot.Tests.cs          - The app's container builds every use case
  FixedTimeProvider.cs              - TimeProvider that always returns the time it was given
  TestDatabase.cs                   - Starts shared SQL Server container; isolated DB per test
  TestHelpers.cs                    - CreatePrincipal, CreateHttpRequest, ValidName / ValidSettings, and Insert/Set/Load helpers for lists, items and members
  AuditLog/
    AuditLog.Http.Tests.cs          - HTTP integration tests verifying audit entries are captured per operation
    CapturingAuditWriter.cs         - In-memory IAuditWriter that accumulates entries for assertion
    ChannelAuditWriter.Tests.cs     - Unit tests for ChannelAuditWriter (batching, flush, failure handling)
    NoOpAuditWriter.cs              - IAuditWriter that discards all entries (used to isolate other tests)
  Checklist/
    CapturingNotifier.cs            - In-memory IChecklistNotifier that records notifications for assertion
    Domain/                         - Tests for the domain values Name and ItemGroupSettingsInput
    Item/                           - Use-case + HTTP tests for the Item slice
    ItemGroup/                      - Use-case + HTTP tests for the ItemGroup slice
    Member/                         - Use-case + HTTP tests for the Member slice
  Http/
    CallerExtensions.Tests.cs       - ToCaller(): user ID and connection ID from a request
  Realtime/
    ChecklistHubTests.cs            - SignalR hub auth tests + full end-to-end notification pipeline tests
  Sql/
    ChecklistSchemaInitializer.Tests.cs- Schema upgrades of databases created before newer columns
    SqlItemGroupStore.Tests.cs      - IItemGroupStore against SQL Server, including the auto-delete rules
    SqlItemStore.Tests.cs           - IItemStore against SQL Server, including completion times
    SqlMembership.Tests.cs          - IMembership against SQL Server, one method at a time
Directory.Build.props               - Solution-wide MSBuild settings and analyzer packages
Directory.Build.targets             - CSharpier format check wired into every CLI build
dotnet-tools.json                   - Pins CSharpier version for local tool restore
global.json                         - Opts `dotnet test` into Microsoft.Testing.Platform
coverage.config                     - Code coverage settings used by CI
specifications/                     - Design and architecture specifications
  architecture.md                   - Backend layout: ports and adapters, where rules live, adding an operation
  checklist.md                      - Checklist feature: domain model, endpoints, DB schema
  accounts.md                       - Accounts: sign-up and sign-in rules, tokens, endpoints, the Users table
  audit-logs.md                     - Audit log feature: schema, implementation design
  authentication.md                 - Authentication: JWT setup, claim resolution
  client.md                         - Web client: structure, sign-in, realtime, accessibility
```

## Specifications

Design and architecture documents for planned and implemented features live in the [`specifications/`](specifications/) folder.

| Document                                              | Description                                                                                                                                        |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| [architecture.md](specifications/architecture.md)     | Backend architecture — the ports-and-adapters layout, where each kind of code and rule belongs, and how to add an operation |
| [checklist.md](specifications/checklist.md)           | Checklist feature — domain model, authorization model, database schema, all API endpoints with request/response shapes, and structural conventions |
| [accounts.md](specifications/accounts.md)             | Accounts — the rules for names, emails and passwords, signing up and in, lockout, the tokens the API issues, the rate limit and the Users table |
| [audit-logs.md](specifications/audit-logs.md)         | Audit Log feature — database schema, per-operation audit data, implementation design, and future extensibility                                     |
| [authentication.md](specifications/authentication.md) | Authentication feature — JWT Bearer setup, token validation behavior, user identity claim resolution, and how users authenticate against the API   |
| [client.md](specifications/client.md)                 | Web client — structure, sign-up and sign-in, data flow, real-time updates, accessibility (WCAG 2.2 AA) and responsive design conventions                   |

## Requirements

- .NET SDK 11
- Docker (or another OCI-compatible runtime)
- Node.js 24 (for the web client; Aspire runs `npm install` and the React Router dev server)

Docker is required because Aspire spins up SQL Server as a persistent container, and Mailpit to catch the app's emails, at startup.

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

   Aspire starts and health-checks these containers before launching the backend:

   | Container  | Image                                        | Port          | Lifetime   |
   | ---------- | -------------------------------------------- | ------------- | ---------- |
   | SQL Server | `mcr.microsoft.com/mssql/server:2025-latest` | auto-assigned | Persistent |
   | Mailpit    | `axllent/mailpit:v1.34.0`                    | auto-assigned | Session    |

   SQL Server is **persistent**, so data survives restarts. Mailpit catches every email the backend sends, such as password reset links: open its inbox from the `mail` resource in the Aspire dashboard. The backend waits for both to be healthy before starting. Database tables are created automatically on first boot.

3. Open the web client at **http://localhost:5173** (see [Using the web client](#using-the-web-client)), or open the Aspire dashboard URL printed in the console to reach the `Api` service and its Scalar UI at `/scalar/v1`.

## Using the web client

**Sign up** with a name, an email address and a password of at least 12 characters, then sign in with the email and password from then on. Emails aren't confirmed, so any address works locally.

- Each browser tab keeps its own sign-in, so you can sign up a second account in another tab to try sharing.
- **Forgot your password?** on the sign-in page emails a link to choose a new one. Locally the email lands in Mailpit's inbox.
- To share a list, ask the other person for their user ID (shown under **Account**) and paste it under **Add a member** on the list page.
- Changes made by other members appear live.
- **Settings** on the list page can hide done items after a while and delete the list once every item has been done for a while (at least a day). Both are off by default and apply to every member.
- Dark mode is the default; use the **Dark mode** toggle in the header to switch to light. Your choice is remembered in the browser.

The client is built to WCAG 2.2 AA (keyboard and screen reader support, visible focus, sufficient contrast in light and dark mode) and works from 320px phone screens up to desktop. Details are in [specifications/client.md](specifications/client.md).

To work on the client without Aspire, start Api, then run `npm install` and `npm run dev` in `Client/`.

## Getting a token locally

The API issues its own tokens. Sign up once (in the web client, or with `/api/auth/sign-up`), then sign in with the same email and password:

```bash
curl -X POST http://localhost:5093/api/auth/sign-in \
  -H "Content-Type: application/json" \
  -d '{"email":"kim@example.com","password":"correct horse battery"}'
```

Copy the returned `accessToken` and supply it as a `Bearer` token in any HTTP client, or paste it into the Bearer scheme in the Scalar UI at `/scalar/v1`. Tokens last 8 hours. The account endpoints allow 20 requests a minute from one address.

## Running without Aspire (alternative setup)

If you prefer a local SQL Server install or an external endpoint, configure the connection string, for example with user secrets (`dotnet user-secrets set ConnectionStrings:database "…" --project Api`):

```json
{
  "ConnectionStrings": {
    "database": "Server=localhost,1433;Database=JustAnotherListDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
  }
}
```

In Development the API signs tokens with the development key in `Api/appsettings.Development.json`. Any other environment must set `Auth:SigningKey` to a secret of its own.

Then run the backend directly:

```bash
dotnet run --project Api
```

The database tables are created automatically on first startup.

## Configuration reference

All runtime configuration for `Api` is resolved from `appsettings.json`, environment variables, and Aspire-injected values.

| Key                          | Required | Description                                                                                                                                    |
| ---------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:database` | Yes      | SQL Server connection string. Injected by Aspire; set manually when running standalone.                                                        |
| `Auth:SigningKey`            | Yes      | A base64-encoded secret of at least 32 random bytes that signs and checks the API's tokens. The app refuses to start without it. Development has one in `appsettings.Development.json`; keep every other environment's secret out of source control. |
| `Auth:Issuer`, `Auth:Audience` | No     | The tokens' `iss` and `aud` claims. Both default to `JustAnotherList`.                                                                         |
| `Auth:TokenLifetime`         | No       | How long a token works. Defaults to 8 hours (`08:00:00`).                                                                                      |
| `Auth:RateLimit:PermitLimit` | No       | Requests per minute one address may make to the account endpoints. Defaults to 20.                                                             |
| `Smtp:Host`, `Smtp:Port`     | No       | The SMTP server for password reset emails. Default `localhost:25`; Aspire points them at Mailpit. Also `Smtp:EnableSsl`, `Smtp:UserName`, `Smtp:Password` and `Smtp:From`. |
| `App:ClientUrl`              | No       | The web client's address, which the links in emails start with. Default `http://localhost:5173`; Aspire sets it.                             |

> **Note:** `launchSettings.json` files are gitignored. Each developer's local launch profiles are not committed.

## Authentication

People sign up and sign in at `/api/auth` with an email address and a password, which ASP.NET Core Identity hashes and checks (five wrong passwords in a row lock an account for five minutes). The answer carries a JWT the API signed with `Auth:SigningKey`, and every other endpoint accepts those tokens and no others. See [specifications/accounts.md](specifications/accounts.md) and [specifications/authentication.md](specifications/authentication.md).

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

The schema is managed without a migration framework. `DatabaseInitializer` runs idempotent SQL on every startup — safe against an existing database, no CLI tooling required. To change the schema, edit `ChecklistSchemaInitializer` (or `AuditLogSchemaInitializer`) directly; `Api/Sql/DatabaseInitializer.cs` runs both.

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

Tests use xUnit against a real SQL Server started by [Testcontainers](https://dotnet.testcontainers.org/) — Docker must be running, but no manually started SQL Server is needed.

```bash
dotnet test
```

Client tests come in four kinds: **unit** tests of pure logic (Vitest in Node), **browser** tests of components in real Chromium (Vitest browser mode), **integration** tests of the production build in Playwright with only the network (REST, the account endpoints and the SignalR WebSocket) faked, and **end-to-end** tests of the whole application with nothing faked. The end-to-end setup starts SQL Server and Mailpit in Docker, runs the real Api and serves the client build, and the tests check collaboration between real users through the backend and SignalR. Pages, dialogs and both themes are checked with axe-core, colour contrast included. See [specifications/client.md](specifications/client.md#testing).

```bash
cd Client
npx playwright install chromium   # once
npm run check      # typecheck + lint + format check + unit, browser and integration tests
npm run test:e2e   # end-to-end: real Api, SQL Server and Mailpit (needs Docker)
```

### How tests work

`Api.Tests` contains two kinds of tests:

**Unit tests** call handler `Execute()` methods directly against a SQL Server test database.

- `Api.Tests` references `Api` internals via `InternalsVisibleTo`, so individual handler `Execute()` methods are called directly — no HTTP server is spun up.
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

# Project Guidelines

## Build and Test

| Task           | Command                                                                        |
| -------------- | ------------------------------------------------------------------------------ |
| Build          | `dotnet build` (runs CSharpier format check; formatting issues fail the build) |
| Test           | `dotnet test` (requires Docker; Testcontainers starts SQL Server)              |
| Run full stack | `dotnet run --project Aspire` (SQL Server + mock OAuth + backend + client)     |
| Format         | `dotnet csharpier .` (local tool; restore with `dotnet tool restore`)          |
| Client check   | `cd Client && npm run check` (typecheck + ESLint + Prettier + all tests)      |
| Client tests   | `npm run test:unit` / `test:browser` / `test:integration` in `Client/`         |
| End-to-end     | `cd Client && npm run test:e2e` (Docker + .NET SDK; real Api, SQL, OAuth)      |
| Client format  | `cd Client && npm run format`                                                  |

## Architecture

- **Aspire/** — .NET Aspire AppHost; orchestrates SQL Server, mock OAuth container, Api backend and Vite client for local dev
- **Checklist/** — the inside of the hexagon: domain records and values, ports and use cases. No library packages and no project references, so it can't depend on ASP.NET Core, Dapper or SignalR
- **Api/** — ASP.NET Core 11 backend (Minimal APIs, Dapper, SQL Server, JWT Bearer auth): the adapters around `Checklist`, grouped by kind (`Http/`, `Realtime/`, `Background/`, `Sql/`, `AuditLog/`), plus the composition root `Program.cs`
- **Api.Tests/** — xUnit tests against SQL Server via Testcontainers (requires Docker)
- **Client/** — React 19 + TypeScript, React Router framework mode in SPA mode (`ssr: false`), SignalR; served at http://localhost:5173 under Aspire

The backend follows a hexagonal (ports and adapters) architecture, in slices (ItemGroup, Item, Member): each operation is a use case in `Checklist/UseCases/<Slice>/` behind an endpoint adapter in `Api/Http/<Slice>/`. See [specifications/architecture.md](specifications/architecture.md) for the layout and where each rule lives, and [specifications/checklist.md](specifications/checklist.md) for the full domain model and API contract.

The client is organized by page (`lists/`, `list/`, `auth/`) with shared `api/`, `realtime/` and `components/`. Routes are declared in `src/routes.ts`; each page file is a route module. See [specifications/client.md](specifications/client.md).

## Conventions

### Backend endpoints

Every operation follows [specifications/architecture.md](specifications/architecture.md):

- Use case in `Checklist/UseCases/<Slice>/`: ports in the constructor, one `Execute(Caller caller, …, CancellationToken ct)` returning a nested `Outcome` enum (commands) or the data, `null` for non-members (queries, and commands whose result the endpoint returns). No HTTP types, SQL, transactions or `AuditContext`
- Values with a rule (`Name`, `ItemGroupSettingsInput`) live in `Checklist/Domain/` with a `TryCreate` that the endpoint adapter calls
- Endpoint adapter in `Api/Http/<Slice>/<Operation>Endpoint.cs`: `internal const string Operation` for `.WithName()` (stored in `AuditLog.Operation`, never change it); `Handle` parses the request (400), calls `user.ToCaller(httpRequest)` (401), runs the use case, maps its outcome to `TypedResults` and sets `AuditContext`
- SQL in a port adapter in `Api/Sql/`. Rules that must hold under concurrency go in one statement or one locked transaction
- Register use cases and adapters in `AddChecklist()` (`ChecklistServiceCollectionExtensions`)
- All Dapper queries use `CommandDefinition` with `CancellationToken`
- SignalR notifications: use cases call `IChecklistNotifier.NotifyXxx()` after mutations, leaving out `caller.ConnectionId`
- Hub at `/hubs/checklist`: `ChecklistHub.JoinGroup` admits only members (through `SubscribeToItemGroup`), uses SignalR groups by `groupId.ToString()`
- Current time: take `TimeProvider` and pass `now` on; don't use `SYSDATETIMEOFFSET()` or `DateTimeOffset.UtcNow`
- Schema changes: add columns to the `CREATE TABLE` and an `IF COL_LENGTH(...) IS NULL ALTER TABLE ... ADD` block in `ChecklistSchemaInitializer` so existing databases upgrade

### Tests

- HTTP integration and SignalR tests: class implements `IClassFixture<ApiFactory>`, uses `factory.CreateClient()` / `factory.CreateHubConnection()`
- `ApiFactory` runs the app on its own scoped `SqlConnection` to an isolated test database (as in production); `factory.Connection` is a separate open connection for arranging and asserting. It keeps the real `ChecklistNotifier` + hub
- Use-case tests: build the use case with the real SQL adapters on a `TestDatabase.CreateAsync()` connection and `CapturingNotifier` (plus a `FixedTimeProvider` when the time matters), and call `Execute` with a `Caller`. Arrange with `db.InsertItemGroup(id, memberIds…)`, `db.InsertItem(...)` and `db.SetItemGroupSettings(...)`, make domain values with `TestHelpers.ValidName(...)` and `ValidSettings(...)`, and check with the `Load…` helpers in `TestHelpers`
- SQL adapter tests go in `Api.Tests/Sql/`, one port method at a time; tests of domain values such as `Name` go in `Api.Tests/Checklist/Domain/`
- `Architecture.Tests.cs` pins the dependency rules (`Checklist` references nothing but .NET, use cases take only ports and `TimeProvider`, driving adapters take no port, SQL adapter or connection), and `CompositionRoot.Tests.cs` checks that the app's container builds every use case. When they fail, fix the code, not the test
- File naming: `*.Http.Tests.cs` (integration) vs `*.Tests.cs` (unit)
- Test auth: `TestAuthHandler` signs requests in as `TestAuthHandler.UserId` (insert a matching `Members` row); act as someone else with `factory.CreateClientFor(userId)` / `factory.CreateHubConnection(userId)`, or as no one with `factory.CreateAnonymousClient()`
- Notification assertions: use `CapturingNotifier` in unit tests, assert on `Notifications` list (type + data + `ExcludeConnectionId`)
- `ApiFactory` removes `ChecklistCleanupService` (`services.RemoveChecklistCleanupService()`); test auto-delete by running the `AutoDeleteItemGroups` use case with a `FixedTimeProvider`

### Client

- **Accessibility is a requirement (WCAG 2.2 AA)**: visible `<label>` for every input, errors via `aria-describedby` + `aria-invalid`, one `<h1 tabIndex={-1}>` per page (and per `ErrorBoundary`) plus `<DocumentTitle>`, icons `aria-hidden` with text alongside
- Route modules: load page data in `clientLoader` via `loadRouteData()`, show load failures in the route's `ErrorBoundary`, type props with `Route.ComponentProps` from `./+types/<File>`. Pages needing a session go under the `ProtectedLayout` layout in `routes.ts` (its `clientMiddleware` redirects)
- Forms that navigate (sign-in, create list) use `<Form>`/`useFetcher` + `clientAction`; list-page mutations call `api/checklists.ts` directly and dispatch reducer actions. Refresh loader data with `useRevalidator().revalidate()`, not by fetching by hand
- Don't touch `window`/`document`/storage at module load: the SPA build prerenders `root.tsx` in Node
- Prefer native elements: `Dialog` (wraps `<dialog>`), `ConfirmDialog` for destructive actions, `popover` for menus, radios in a `<fieldset>` for segmented controls
- Never disable a button that may hold focus; use `aria-disabled` and guard the handler
- When an element holding focus is removed, move focus somewhere meaningful (see `planFocusAfterRemoval` in `ListPage.tsx`)
- Feedback through `useToast()`: `notify()` for visible messages (errors stay until dismissed), `announce()` for screen reader–only messages
- New colors go into the tokens in `styles/global.css`, in both the dark (default) and `[data-theme="light"]` blocks, meeting 4.5:1 (text) / 3:1 (UI). Never hard-code colors in components
- API calls go through `api/checklists.ts`; local changes and SignalR events dispatch the same idempotent reducer actions
- Realtime: subscribe with `useGroupEvents(listIds, handlers)`; never create another `HubConnection`
- Tests come in three kinds (details in specifications/client.md#testing); never use `vi.mock`, module mocks or jsdom:
  - **Unit** `src/**/*.unit.test.ts`: pure logic in Node, nothing faked
  - **Browser** `src/**/*.browser.test.tsx`: one component/module in real Chromium (`vitest-browser-react`, `page`/`userEvent` from `vitest/browser`); only prop callbacks and, if needed, timers are faked; `expectNoAxeViolations()` checks contrast too
  - **Integration** `integration/*.spec.ts`: Playwright against the production build; only the network (`FakeBackend`: REST, OAuth, SignalR over `routeWebSocket`) and timers may be faked. Use `app.signIn()`/`app.openList()`, `backend.as(OTHER)` for other members, `app.subscribed(id)` before their changes, `expectAccessible(page)` for new pages and dialogs
  - **End-to-end** `e2e/*.spec.ts`: Playwright against the whole application started by `e2e/support/stack.ts` (SQL Server + mock OAuth in Testcontainers, real Api, production client via `vite preview`); nothing faked. Use `newUser()` (fresh random user in its own browser context) and its UI helpers, `receivingLiveUpdates(id)` before relying on another member's change, and `runSql()` only to arrange what the UI can't
- Query by role and accessible name, and assert focus, the way assistive technology sees the page

## Pitfalls

- `dotnet build` fails on formatting issues (CSharpier). Run `dotnet csharpier .` to fix before committing.
- **Central package management** via `Directory.Packages.props` — project `PackageReference` items must omit `Version`. Update versions only in `Directory.Packages.props`.
- **Warnings as errors** — all compiler warnings (including Roslynator/Meziantou analyzers) fail the build.
- **Nullable reference types** enabled everywhere; do not add `#nullable disable`.
- Tests run on **xUnit v3 + Microsoft.Testing.Platform** (`global.json`). Pass `TestContext.Current.CancellationToken` to any call accepting a `CancellationToken` (analyzer xUnit1051 fails the build otherwise).
- Tests need Docker running: `TestDatabase` starts one shared SQL Server container per run and creates an isolated database per test.
- SQL Server 2022 or later is required: list settings use `IS DISTINCT FROM` and `GREATEST`.
- `TestDatabase` connections are already **open**, but the app's scoped `SqlConnection` starts **closed**. Dapper opens it per command, but `BeginTransaction` needs `db.Open()` first.
- CI runs the backend tests with code coverage (`coverage.config`). On Linux that rewrites `Api.dll` and `Checklist.dll` before they load, which adds references such as `netstandard`, so tests that inspect assemblies must allow for it. To reproduce on Windows, pass `dotnet test --coverage --coverage-settings` a copy of `coverage.config` with `<EnableStaticManagedInstrumentation>True</EnableStaticManagedInstrumentation>` and `<EnableDynamicManagedInstrumentation>False</EnableDynamicManagedInstrumentation>` in `<CodeCoverage>`.
- Client: `eslint-plugin-jsx-a11y` doesn't declare ESLint 10 support yet; `package.json` `overrides` allows it. TypeScript stays on 6.0.x until typescript-eslint supports newer versions.
- Client: `global.css` must be the first import in `src/root.tsx` so CSS Modules can override it.
- Client: route types are generated. `npm run typecheck` runs `react-router typegen` first; run it after adding or renaming a route module, or `./+types/...` imports won't resolve.
- Client: React Router runs a navigation's loaders in parallel; put checks that must run first (like auth) in `clientMiddleware`.
- Client: browser, integration and end-to-end tests need Playwright's Chromium: `npx playwright install chromium` once (CI uses `--with-deps`).
- Client: end-to-end tests need Docker running and port 4174 free; server logs go to `Client/test-results/e2e-logs/`. Keep the images in `e2e/support/stack.ts` in step with `Aspire/Program.cs`.
- Client: integration tests run against `react-router build`. Locally Playwright reuses a server already on port 4173, so stop stray servers if results look stale.
- Client: axe-core uses `setTimeout`, so it hangs while a test fakes timers. Fake only the timers you need, and run axe with real ones.
- Client: Playwright treats `aria-disabled` as disabled and won't click it; use `click({ force: true })` to test that such clicks are ignored.
- Client: don't set `skipNegotiation` on the SignalR connection. The connection ID comes only from negotiate; without it the client can't send `X-SignalR-Connection-Id` and the server echoes your own changes back.

## Specifications

Detailed design docs live in [specifications/](specifications/):

- [architecture.md](specifications/architecture.md) — Backend layout (ports and adapters), where code and rules belong, how to add an operation
- [checklist.md](specifications/checklist.md) — Domain model, endpoints, DB schema, authorization rules
- [audit-logs.md](specifications/audit-logs.md) — Audit schema, per-operation capture, ChannelAuditWriter design
- [authentication.md](specifications/authentication.md) — JWT Bearer setup, claim resolution, failure modes
- [client.md](specifications/client.md) — Client structure, dev sign-in, realtime, accessibility and responsive conventions

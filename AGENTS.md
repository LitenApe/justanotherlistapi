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
| End-to-end     | `cd Client && npm run test:e2e` (Docker + .NET SDK; real Core, SQL, OAuth)     |
| Client format  | `cd Client && npm run format`                                                  |

## Architecture

- **Aspire/** — .NET Aspire AppHost; orchestrates SQL Server, mock OAuth container, Core backend and Vite client for local dev
- **Core/** — ASP.NET Core 10 backend (Minimal APIs, Dapper, SQL Server, JWT Bearer auth)
- **Core.Tests/** — xUnit tests against SQL Server via Testcontainers (requires Docker)
- **Client/** — React 19 + TypeScript, React Router framework mode in SPA mode (`ssr: false`), SignalR; served at http://localhost:5173 under Aspire

Backend features are organized as vertical slices under `Core/Checklist/`. Each endpoint is a static class with `MapEndpoint()` + `Execute()` methods. See [specifications/checklist.md](specifications/checklist.md) for the full domain model and API contract.

The client is organized by page (`lists/`, `list/`, `auth/`) with shared `api/`, `realtime/` and `components/`. Routes are declared in `src/routes.ts`; each page file is a route module. See [specifications/client.md](specifications/client.md).

## Conventions

### Backend endpoints

- One static class per endpoint (e.g., `CreateItem.cs`) with `MapEndpoint` and `Execute` methods
- Use `Results<Ok<T>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>` typed results
- Authorization via `db.ExecuteAsItemGroupMember<T>(...)` or `db.ExecuteAsAuthenticatedUser<T>(...)` extension methods
- Separate internal `CreateData`/`LoadData` methods for testable data logic
- Audit context: set `auditContext.SubResourceId` in handlers when a sub-resource is created
- All Dapper queries use `CommandDefinition` with `CancellationToken`
- SignalR notifications: inject `IChecklistNotifier` and call `NotifyXxx()` after mutations
- Read `X-SignalR-Connection-Id` from `httpRequest.Headers` and pass as `excludeConnectionId`
- Hub at `/hubs/checklist`: `ChecklistHub` validates membership in `JoinGroup`, uses SignalR groups by `groupId.ToString()`
- Current time: take `TimeProvider` in `Execute` and pass `now` into data methods; don't use `SYSDATETIMEOFFSET()` or `DateTimeOffset.UtcNow`
- Schema changes: add columns to the `CREATE TABLE` and an `IF COL_LENGTH(...) IS NULL ALTER TABLE ... ADD` block in `ChecklistSchemaInitializer` so existing databases upgrade

### Tests

- HTTP integration tests: class implements `IClassFixture<ApiFactory>`, uses `factory.CreateClient()`
- SignalR end-to-end tests: class implements `IClassFixture<SignalRApiFactory>`, connects `HubConnection` via `factory.Server.CreateHandler()`
- Unit tests for data logic: call internal static methods directly with a `TestDatabase.CreateAsync()` connection
- File naming: `*.Http.Tests.cs` (integration) vs `*.Tests.cs` (unit)
- Test auth: `TestAuthHandler` provides a fixed `UserId`; insert matching `Members` row for authorization
- Notification assertions: use `CapturingNotifier` in unit tests, assert on `Notifications` list (type + data + `ExcludeConnectionId`)
- `ApiFactory` replaces `IChecklistNotifier` with `CapturingNotifier`; `SignalRApiFactory` keeps the real `ChecklistNotifier` + hub
- Both factories remove `ChecklistCleanupService` (`services.RemoveChecklistCleanupService()`); test auto-delete by calling `AutoDeleteItemGroups` with a fixed `now`

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
  - **End-to-end** `e2e/*.spec.ts`: Playwright against the whole application started by `e2e/support/stack.ts` (SQL Server + mock OAuth in Testcontainers, real Core, production client via `vite preview`); nothing faked. Use `newUser()` (fresh random user in its own browser context) and its UI helpers, `receivingLiveUpdates(id)` before relying on another member's change, and `runSql()` only to arrange what the UI can't
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

- [checklist.md](specifications/checklist.md) — Domain model, endpoints, DB schema, authorization rules
- [audit-logs.md](specifications/audit-logs.md) — Audit schema, per-operation capture, ChannelAuditWriter design
- [authentication.md](specifications/authentication.md) — JWT Bearer setup, claim resolution, failure modes
- [client.md](specifications/client.md) — Client structure, dev sign-in, realtime, accessibility and responsive conventions

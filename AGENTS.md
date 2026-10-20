# Project Guidelines

## Build and Test

| Task           | Command                                                                        |
| -------------- | ------------------------------------------------------------------------------ |
| Build          | `dotnet build` (runs CSharpier format check; formatting issues fail the build) |
| Test           | `dotnet test` (requires Docker; Testcontainers starts SQL Server)              |
| Run full stack | `dotnet run --project Aspire` (SQL Server + mock OAuth + backend + client)     |
| Format         | `dotnet csharpier .` (local tool; restore with `dotnet tool restore`)          |
| Client check   | `cd Client && npm run check` (typecheck + ESLint + Prettier check + Vitest)    |
| Client format  | `cd Client && npm run format`                                                  |

## Architecture

- **Aspire/** — .NET Aspire AppHost; orchestrates SQL Server, mock OAuth container, Core backend and Vite client for local dev
- **Core/** — ASP.NET Core 10 backend (Minimal APIs, Dapper, SQL Server, JWT Bearer auth)
- **Core.Tests/** — xUnit tests against SQL Server via Testcontainers (requires Docker)
- **Client/** — React 19 + TypeScript SPA (Vite, React Router, SignalR); served at http://localhost:5173 under Aspire

Backend features are organized as vertical slices under `Core/Checklist/`. Each endpoint is a static class with `MapEndpoint()` + `Execute()` methods. See [specifications/checklist.md](specifications/checklist.md) for the full domain model and API contract.

The client is organized by page (`lists/`, `list/`, `auth/`) with shared `api/`, `realtime/` and `components/`. See [specifications/client.md](specifications/client.md).

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

- **Accessibility is a requirement (WCAG 2.2 AA)**: visible `<label>` for every input, errors via `aria-describedby` + `aria-invalid`, one `<h1 tabIndex={-1}>` per page plus `useDocumentTitle`, icons `aria-hidden` with text alongside
- Prefer native elements: `Dialog` (wraps `<dialog>`), `ConfirmDialog` for destructive actions, `popover` for menus, radios in a `<fieldset>` for segmented controls
- Never disable a button that may hold focus; use `aria-disabled` and guard the handler
- When an element holding focus is removed, move focus somewhere meaningful (see `planFocusAfterRemoval` in `ListPage.tsx`)
- Feedback through `useToast()`: `notify()` for visible messages (errors stay until dismissed), `announce()` for screen reader–only messages
- New colors go into the tokens in `styles/global.css`, in both the dark (default) and `[data-theme="light"]` blocks, meeting 4.5:1 (text) / 3:1 (UI). Never hard-code colors in components
- API calls go through `api/checklists.ts`; local changes and SignalR events dispatch the same idempotent reducer actions
- Realtime: subscribe with `useGroupEvents(listIds, handlers)`; never create another `HubConnection`
- Tests: `renderApp(path)` + `installFakeApi(lists)`; query by role/name; call `expectNoAxeViolations()` for new pages and dialog states; emit hub events with `latestConnection().emit(...)`

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
- Client: `global.css` must be imported before components in `main.tsx` so CSS Modules can override it.
- Client: jsdom has no `HTMLDialogElement.showModal()`; `src/test/setup.ts` polyfills it.

## Specifications

Detailed design docs live in [specifications/](specifications/):

- [checklist.md](specifications/checklist.md) — Domain model, endpoints, DB schema, authorization rules
- [audit-logs.md](specifications/audit-logs.md) — Audit schema, per-operation capture, ChannelAuditWriter design
- [authentication.md](specifications/authentication.md) — JWT Bearer setup, claim resolution, failure modes
- [client.md](specifications/client.md) — Client structure, dev sign-in, realtime, accessibility and responsive conventions

# Project Guidelines

## Build and Test

| Task           | Command                                                                        |
| -------------- | ------------------------------------------------------------------------------ |
| Build          | `dotnet build` (runs CSharpier format check; formatting issues fail the build) |
| Test           | `dotnet test --project Core.Tests`                                             |
| Run full stack | `dotnet run --project Aspire` (SQL Server + mock OAuth + backend)              |
| Format         | `dotnet csharpier .` (local tool; restore with `dotnet tool restore`)          |

## Architecture

- **Aspire/** — .NET Aspire AppHost; orchestrates SQL Server, mock OAuth container and Core backend for local dev
- **Core/** — ASP.NET Core 10 backend (Minimal APIs, Dapper, SQL Server, JWT Bearer auth)
- **Core.Tests/** — xUnit tests with in-memory SQLite (no running server needed)

Backend features are organized as vertical slices under `Core/Checklist/`. Each endpoint is a static class with `MapEndpoint()` + `Execute()` methods. See [specifications/checklist.md](specifications/checklist.md) for the full domain model and API contract.

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

### Tests

- HTTP integration tests: class implements `IClassFixture<ApiFactory>`, uses `factory.CreateClient()`
- SignalR end-to-end tests: class implements `IClassFixture<SignalRApiFactory>`, connects `HubConnection` via `factory.Server.CreateHandler()`
- Unit tests for data logic: call internal static methods directly with SQLite connection
- File naming: `*.Http.Tests.cs` (integration) vs `*.Tests.cs` (unit)
- Test auth: `TestAuthHandler` provides a fixed `UserId`; insert matching `Members` row for authorization
- Notification assertions: use `CapturingNotifier` in unit tests, assert on `Notifications` list (type + data + `ExcludeConnectionId`)
- `ApiFactory` replaces `IChecklistNotifier` with `CapturingNotifier`; `SignalRApiFactory` keeps the real `ChecklistNotifier` + hub

## Pitfalls

- `dotnet build` fails on formatting issues (CSharpier). Run `dotnet csharpier .` to fix before committing.
- **Central package management** via `Directory.Packages.props` — project `PackageReference` items must omit `Version`. Update versions only in `Directory.Packages.props`.
- **Warnings as errors** — all compiler warnings (including Roslynator/Meziantou analyzers) fail the build.
- **Nullable reference types** enabled everywhere; do not add `#nullable disable`.
- SQLite tests need a `GuidTypeHandler` registered (already handled in `TestDatabase.cs`).

## Specifications

Detailed design docs live in [specifications/](specifications/):

- [checklist.md](specifications/checklist.md) — Domain model, endpoints, DB schema, authorization rules
- [audit-logs.md](specifications/audit-logs.md) — Audit schema, per-operation capture, ChannelAuditWriter design
- [authentication.md](specifications/authentication.md) — JWT Bearer setup, claim resolution, failure modes
- [dev-tools.md](specifications/dev-tools.md) — DevPanel, chaos controls, seed endpoint

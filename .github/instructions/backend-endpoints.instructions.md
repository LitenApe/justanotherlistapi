---
description: "Use when creating or modifying backend API endpoints, use cases, ports or adapters in Api/ or Checklist/"
applyTo: "Api/**/*.cs,Checklist/**/*.cs"
---

# Backend Endpoint Conventions

The backend is moving to ports and adapters one slice at a time; see [specifications/architecture.md](../../specifications/architecture.md) for the layout and the migration status. New and migrated operations use the new layout. The slices not migrated yet keep the old one until their turn.

## New layout (Member and Item slices so far)

```csharp
// Checklist/UseCases/<Slice>/VerbNoun.cs: no HTTP types, no SQL
public sealed class VerbNoun(IMembership membership, IChecklistNotifier notifier)
{
    public enum Outcome { Done, NotAMember }

    public async Task<Outcome> Execute(Caller caller, Guid itemGroupId, CancellationToken ct) { ... }
}

// Api/Http/<Slice>/VerbNounEndpoint.cs
internal static class VerbNounEndpoint
{
    internal const string Operation = "VerbNoun"; // stored in AuditLog.Operation: never change it

    internal static void MapEndpoint(this IEndpointRouteBuilder builder) { ... .WithName(Operation); }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, ForbidHttpResult>> Handle(
        Guid itemGroupId, ClaimsPrincipal user, HttpRequest httpRequest, VerbNoun useCase, CancellationToken ct) { ... }
}
```

- Use cases take their ports in the constructor and return a nested `Outcome` enum (commands) or the data, `null` for non-members (queries). They never see `AuditContext` or manage transactions
- Values with a rule, such as `Name` (never blank), live in `Checklist/Domain/` with a `TryCreate` that the endpoint calls. Use cases that need the current time take `TimeProvider` and pass `now` to the port
- `Handle` parses the request into domain values first (such as `Name.TryCreate`; 400), then calls `user.ToCaller(httpRequest)` (401), runs the use case and maps its outcome to `TypedResults`. It sets `AuditContext` when the route doesn't carry an ID
- Ports live in `Checklist/Ports/` and their SQL adapters in `Api/Sql/`. Keep rules that must hold under concurrency in one statement or one locked transaction
- Register use cases and adapters in `AddChecklist()`, map the endpoint in `MapChecklistApi()`, and add its name to `AuditEndpointFilter` and to `auditedEndpoints` in `AuditLog.Http.Tests.cs`

## Old layout (ItemGroup slice, until migrated)

Each endpoint is a single static class with `MapEndpoint`, an `Execute` handler taking `ClaimsPrincipal`, `IDbConnection` and `AuditContext`, `internal static` `CreateData`/`LoadData` methods, and a nested `Request` record. Authorize via `db.ExecuteAsItemGroupMember<T>(...)` or `claimsPrincipal.ExecuteAsAuthenticatedUser<T>(...)`.

## Both

- Return `Results<...>` typed results; never throw for expected failures
- Use `CommandDefinition` with `CancellationToken` for all Dapper queries
- Add `.WithSummary()`, `.WithDescription()`, `.WithTags()`, `.WithName()` to route registration

See [specifications/checklist.md](../../specifications/checklist.md) for the full API contract.

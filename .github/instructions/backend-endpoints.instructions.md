---
description: "Use when creating or modifying backend API endpoints, handlers, or Checklist feature files in Core/"
applyTo: "Core/Checklist/**/*.cs"
---

# Backend Endpoint Conventions

Each endpoint is a single static class with this structure:

```csharp
public static class VerbNoun
{
    public static void MapEndpoint(this IEndpointRouteBuilder builder) { ... }

    public static async Task<Results<Ok<T>, BadRequest, UnauthorizedHttpResult, ForbidHttpResult>> Execute(
        Guid itemGroupId,
        Request request,
        ClaimsPrincipal claimsPrincipal,
        IDbConnection db,
        AuditContext auditContext,
        CancellationToken ct = default) { ... }

    internal static async Task<T> CreateData(...) { ... }  // or LoadData

    public record Request { ... }
}
```

## Rules

- Return `Results<...>` typed results — never throw for expected failures
- Authorize via `db.ExecuteAsItemGroupMember<T>(...)` or `db.ExecuteAsAuthenticatedUser<T>(...)`
- Set `auditContext.SubResourceId` when creating a sub-resource
- Use `CommandDefinition` with `CancellationToken` for all Dapper queries
- Validate inputs at the top of `Execute`; return `TypedResults.BadRequest()` on failure
- Keep data access in a separate `internal static` method for unit testability
- Add `.WithSummary()`, `.WithDescription()`, `.WithTags()`, `.WithName()` to route registration

See [specifications/checklist.md](../../specifications/checklist.md) for the full API contract.

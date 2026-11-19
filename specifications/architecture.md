# Backend Architecture

## Table of Contents

- [Overview](#overview)
- [Projects](#projects)
- [The Inside: Checklist](#the-inside-checklist)
  - [Domain](#domain)
  - [Ports](#ports)
  - [Use Cases](#use-cases)
- [The Outside: Api](#the-outside-api)
  - [HTTP Endpoints](#http-endpoints)
  - [SQL Server](#sql-server)
  - [Realtime](#realtime)
  - [Composition Root](#composition-root)
- [Where the Rules Live](#where-the-rules-live)
- [Testing](#testing)
- [Adding an Operation](#adding-an-operation)
- [Migration Status](#migration-status)

---

## Overview

The backend is organized as a hexagonal (ports and adapters) architecture. The domain and its use cases live in the `Checklist` project, which knows nothing about HTTP, SQL Server or SignalR. Everything that talks to the outside world lives around it in `Api` and depends on it, never the other way round.

```
                 driving adapters (Api)
       HTTP endpoints · SignalR hub · cleanup job
                          │ call
                          ▼
   ┌──────────────── Checklist (the inside) ────────────────┐
   │  use cases · domain records and rules · ports          │
   └────────────────────────────────────────────────────────┘
                          ▲ implement
                          │
        SQL Server · SignalR broadcasts · audit log · clock
                  driven adapters (Api)
```

`Api/Program.cs` is the composition root: the one place that knows both sides and plugs each adapter into its port.

---

## Projects

| Project      | Role                                                                                                   | References                                                         |
| ------------ | ------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------ |
| `Checklist/` | The inside: domain records, use cases and the ports they need                                         | Nothing: no library packages and no other project                  |
| `Api/`       | The outside: adapters (HTTP endpoints, SQL Server, SignalR, audit log, background job) and `Program.cs` | `Checklist`, ASP.NET Core, Dapper, SignalR, OpenTelemetry, Scalar |
| `Api.Tests/` | Tests for both                                                                                         | `Api`                                                              |
| `Aspire/`    | Local orchestration                                                                                    | `Api`, by path (`AddDotnetProject`)                                |

Because `Checklist` has no package references, the compiler stops the inside from depending on the outside. The analyzer packages that `Directory.Build.props` adds to every project don't count: they only run at build time.

---

## The Inside: Checklist

| Folder       | Contents                                                                                          |
| ------------ | ------------------------------------------------------------------------------------------------- |
| `Domain/`    | The records the API and SignalR send (`ItemGroup`, `ItemGroupSettings`, `Item`), and `Caller`     |
| `Ports/`     | The interfaces the inside needs from the outside: `IMembership`, `IChecklistNotifier`             |
| `UseCases/`  | One class per operation, in a folder per slice. Namespace `Checklist.UseCases` for all of them    |

### Domain

The domain records keep their `[Description]` attributes (plain `System.ComponentModel`, used for the OpenAPI document) and stay the JSON and SignalR payloads. Separate HTTP DTOs only pay off once the shapes need to differ.

`Caller(UserId, ConnectionId)` says who is asking. A driving adapter builds it: endpoints with `ToCaller`, the hub from its connection. `ConnectionId` is the caller's SignalR connection, if any. Use cases never interpret it; they only pass it to the notifier, so the sender doesn't get its own change back.

### Ports

Each port is named after what the inside needs, and each method covers one atomic operation, so use cases never manage transactions.

| Port                 | Methods                                                                                                      | Adapter                       |
| -------------------- | ------------------------------------------------------------------------------------------------------------ | ----------------------------- |
| `IMembership`        | `IsMember`, `ListMembers`, `Add` (false if already a member), `RemoveUnlessLast` (false for the last member) | `SqlMembership`               |
| `IChecklistNotifier` | `NotifyItemCreated` … `NotifyGroupDeleted`, each with the connection to leave out                           | `ChecklistNotifier` (SignalR) |

### Use Cases

- A sealed class per operation, with its ports in the constructor and one method: `Execute(Caller caller, …, CancellationToken ct)`.
- Commands return a nested `Outcome` enum, such as `RemoveMember.Outcome.LastMember`. Queries return the data, or `null` when the caller isn't a member.
- Membership comes first, through `IMembership.IsMember`.
- Notifications go out after the change, with `caller.ConnectionId` as the connection to leave out.
- No HTTP types, no SQL, no transactions, no `AuditContext`.

```csharp
public sealed class RemoveMember(IMembership membership, IChecklistNotifier notifier)
{
    public enum Outcome { Removed, NotAMember, LastMember }

    public async Task<Outcome> Execute(Caller caller, Guid itemGroupId, Guid memberId, CancellationToken ct)
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
            return Outcome.NotAMember;

        if (!await membership.RemoveUnlessLast(itemGroupId, memberId, ct))
            return Outcome.LastMember;

        await notifier.NotifyMemberRemoved(itemGroupId, memberId, caller.ConnectionId);
        return Outcome.Removed;
    }
}
```

---

## The Outside: Api

### HTTP Endpoints

Each endpoint adapter is an `internal static class` in `Api/Http/<Slice>/`, named after its operation plus `Endpoint` (`RemoveMemberEndpoint`), in namespace `Api.Http`.

- `internal const string Name` is passed to `.WithName(Name)`. `AuditLog.Operation` stores it, so never change it.
- `MapEndpoint` registers the route with its summary, description and tag.
- A private `Handle` does four things, in this order:
  1. Parse the request into domain values, or return 400. Parsing comes first, so invalid input gets 400 before 401, as [checklist.md](checklist.md#validation) requires.
  2. Resolve the caller with `user.ToCaller(httpRequest)`, or return 401. `ToCaller` reads the user ID claim and the `X-SignalR-Connection-Id` header.
  3. Call the use case.
  4. Map its outcome to `TypedResults`.
- It fills in the `AuditContext` fields the route doesn't carry: `TargetUserId` first (so refusals carry it too), and new IDs on success only. See [audit-logs.md](audit-logs.md#auditcontext).

### SQL Server

SQL adapters in `Api/Sql/` implement the ports with Dapper, always through `CommandDefinition` with the `CancellationToken`. Rules that must hold under concurrency are enforced here, in one statement or one transaction:

- `SqlMembership.Add` is one conditional insert whose existence check holds `UPDLOCK, HOLDLOCK` until the insert. Of two concurrent adds of the same member, the second waits, finds the row and returns `false`.
- `SqlMembership.RemoveUnlessLast` takes an update lock on the list's `ItemGroups` row, then checks and deletes in the same transaction, so removals from one list take turns.

Moving such rules into C# would mean loading, changing and saving a row, with optimistic concurrency on top: more code and more round trips for the same behavior. So the domain stays thin.

### Realtime

`ChecklistHub.JoinGroup` builds a `Caller` from the connection and asks the `SubscribeToItemGroup` use case whether it may receive the list's updates; only then does it add the connection to the SignalR group. `ChecklistNotifier` implements `IChecklistNotifier` with `IHubContext`.

### Composition Root

`Program.cs` calls `AddChecklist()` (`ChecklistServiceCollectionExtensions`), which registers every use case and binds each port to its adapter. Use cases and SQL adapters are scoped, so they share the request's connection; the notifier is a singleton.

---

## Where the Rules Live

| Rule                                       | Where                                                          |
| ------------------------------------------ | -------------------------------------------------------------- |
| Only members may read or change a list     | Use cases, through `IMembership.IsMember`; the hub, likewise   |
| A list always keeps at least one member    | `SqlMembership.RemoveUnlessLast` (locked transaction)          |
| A user is a member at most once            | `SqlMembership.Add` (one conditional insert)                   |
| Who triggered a change doesn't get it back | `Caller.ConnectionId`, passed to the notifier by use cases     |

---

## Testing

All backend tests run against a real SQL Server (Testcontainers). There are no in-memory fakes of the ports, because they would have to copy the rules the SQL adapters enforce.

| Kind              | Where                                         | What it covers                                                                                                  |
| ----------------- | --------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| Use-case tests    | `Api.Tests/Checklist/<Slice>/<Operation>.Tests.cs` | The use case built with the real SQL adapters on a `TestDatabase` connection and `CapturingNotifier`: outcome, stored data, notifications |
| SQL adapter tests | `Api.Tests/Sql/`                              | One port method at a time, including the rules in the table above                                               |
| Adapter helpers   | `Api.Tests/Http/`                             | Small pieces of the HTTP adapter, such as `ToCaller`                                                            |
| HTTP tests        | `*.Http.Tests.cs`                             | The whole app through `ApiFactory`: routes, status codes, auditing, and races between concurrent requests      |

---

## Adding an Operation

1. Write the use case in `Checklist/UseCases/<Slice>/`, adding port methods if it needs them.
2. Implement any new port methods in the SQL adapter in `Api/Sql/`.
3. Write the endpoint adapter in `Api/Http/<Slice>/` and map it in `MapChecklistApi()`.
4. Register the use case, and any new adapter, in `AddChecklist()`.
5. Add the endpoint to the audit resource types in `AuditEndpointFilter` and to `auditedEndpoints` in `AuditLog.Http.Tests.cs` (see [audit-logs.md](audit-logs.md#adding-audit-logging-to-a-new-endpoint)).
6. Add use-case, adapter and HTTP tests.

---

## Migration Status

The backend is moving to this layout one step at a time, and every step keeps all tests passing. Until a slice is migrated, its endpoints still follow the static `MapEndpoint` + `Execute` pattern described in [checklist.md](checklist.md#structural-conventions).

| Step                                                                                     | State       |
| ---------------------------------------------------------------------------------------- | ----------- |
| Rename `Core` to `Api` (in hexagonal terms, "core" means the inside)                    | Done        |
| `Checklist` project with the domain records and the `IChecklistNotifier` port           | Done        |
| Member slice: `Caller`, `IMembership`, use cases, endpoint adapters, hub                | Done        |
| Item slice                                                                               | Not started |
| ItemGroup slice, auto-delete and the cleanup service                                    | Not started |
| Adapters grouped by kind, old helpers removed, architecture test                        | Not started |

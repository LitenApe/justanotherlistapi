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
  - [Cleanup Service](#cleanup-service)
  - [Composition Root](#composition-root)
- [Where the Rules Live](#where-the-rules-live)
- [Testing](#testing)
- [Adding an Operation](#adding-an-operation)

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

| Folder      | Contents                                                                                                                                                                |
| ----------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Domain/`   | The records the API and SignalR send (`ItemGroup`, `ItemGroupSettings`, `Item`), `Caller`, and the values use cases take: `Name`, `ItemInput`, `ItemGroupSettingsInput` |
| `Ports/`    | The interfaces the inside needs from the outside: `IMembership`, `IItemGroupStore`, `IItemStore`, `IChecklistNotifier`, `IAuditTrail`                                   |
| `UseCases/` | One class per operation, in a folder per slice (`ItemGroup`, `Item`, `Member`). Namespace `Checklist.UseCases` for all of them                                          |

### Domain

The domain records keep their `[Description]` attributes (plain `System.ComponentModel`, used for the OpenAPI document) and stay the JSON and SignalR payloads. Separate HTTP DTOs only pay off once the shapes need to differ.

`Caller(UserId, ConnectionId)` says who is asking. A driving adapter builds it: endpoints with `ToCaller`, the hub from its connection. `ConnectionId` is the caller's SignalR connection, if any. Use cases never interpret it; they only pass it to the notifier, so the sender doesn't get its own change back.

`Name` is a list or item name. `Name.TryCreate` is the only way to make one and refuses blank names, so a use case can't be handed a blank name: endpoint adapters parse names first and answer 400 when that fails. `ItemInput(Name, Description, IsComplete)` is what a member sets on an item when adding or editing it.

`ItemGroupSettingsInput` is the same idea for a list's settings: `TryCreate` accepts each one off (`null`) or within its range, auto-delete from one day and auto-hide from zero minutes, both up to a year. When the auto-delete countdown starts is up to the store, so the input has no timestamp.

### Ports

Each port is named after what the inside needs, and each method covers one atomic operation, so use cases never manage transactions.

| Port                 | Methods                                                                                                                                            | Adapter                       |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------- |
| `IMembership`        | `IsMember`, `ListMembers`, `Add` (false if already a member), `RemoveUnlessLast` (false for the last member)                                       | `SqlMembership`               |
| `IItemGroupStore`    | `Create` (with its first member), `Load`, `ListForMember`, `Rename`, `UpdateSettings` (returns them as stored), `Delete`, `DeleteDueForAutoDelete` | `SqlItemGroupStore`           |
| `IItemStore`         | `Create`, `Update` (returns the item as stored, `null` when it's gone), `Delete`; items in other lists are left alone                              | `SqlItemStore`                |
| `IChecklistNotifier` | `NotifyItemCreated` … `NotifyGroupDeleted`, each with the connection to leave out                                                                  | `ChecklistNotifier` (SignalR) |
| `IAuditTrail`        | `ItemGroupAutoDeleted`: audit entries that no request is behind                                                                                    | `AuditTrail` (audit log)      |

### Use Cases

- A sealed class per operation, with its ports in the constructor and one method: `Execute(Caller caller, …, CancellationToken ct)`.
- Commands return a nested `Outcome` enum, such as `RemoveMember.Outcome.LastMember`. Queries return the data, or `null` when the caller isn't a member.
- Membership comes first, through `IMembership.IsMember`.
- Notifications go out after the change, with `caller.ConnectionId` as the connection to leave out.
- The current time comes from `TimeProvider`, which is part of .NET, and goes to ports as `now`.
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

The adapters are grouped by kind, each kind in a folder and namespace of its own:

| Folder            | Adapters                                                                                                                                |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `Api/Http/`       | Driving: the endpoint adapters, a folder per slice, with `ToCaller` and `MapChecklistApi()`                                             |
| `Api/Realtime/`   | Driving: `ChecklistHub`. Driven: `ChecklistNotifier`, for `IChecklistNotifier`                                                          |
| `Api/Background/` | Driving: `ChecklistCleanupService`                                                                                                      |
| `Api/Sql/`        | Driven: `SqlItemGroupStore`, `SqlItemStore`, `SqlMembership`; the schema (`DatabaseInitializer`, `ChecklistSchemaInitializer`)          |
| `Api/AuditLog/`   | The audit log: `AuditEndpointFilter` around every endpoint, `AuditTrail` for `IAuditTrail`, `ChannelAuditWriter` and the table's schema |
| `Api/`            | `Program.cs` and `AddChecklist()`: the composition root                                                                                 |

### HTTP Endpoints

Each endpoint adapter is an `internal static class` in `Api/Http/<Slice>/`, named after its operation plus `Endpoint` (`RemoveMemberEndpoint`), in namespace `Api.Http`.

- `internal const string Operation` is passed to `.WithName(Operation)`. `AuditLog.Operation` stores it, so never change it. (A constant called `Name` would hide the `Name` domain type inside the class.)
- `MapEndpoint` registers the route with its summary, description and tag.
- A request body is a nested `Request` record whose `[Description]` attributes document it in OpenAPI. Only `Handle` sees it.
- A private `Handle` does four things, in this order:
  1. Parse the request into domain values (such as `Name.TryCreate`), or return 400. Parsing comes first, so invalid input gets 400 before 401, as [checklist.md](checklist.md#validation) requires.
  2. Resolve the caller with `user.ToCaller(httpRequest)`, or return 401. `ToCaller` reads the user ID claim and the `X-SignalR-Connection-Id` header.
  3. Call the use case.
  4. Map its outcome to `TypedResults`.
- It fills in the `AuditContext` fields the route doesn't carry: `TargetUserId` first (so refusals carry it too), and new IDs on success only. See [audit-logs.md](audit-logs.md#auditcontext).

### SQL Server

SQL adapters in `Api/Sql/` implement the ports with Dapper, always through `CommandDefinition` with the `CancellationToken`. Rules that must hold under concurrency are enforced here, in one statement or one transaction:

- `SqlMembership.Add` is one conditional insert whose existence check holds `UPDLOCK, HOLDLOCK` until the insert. Of two concurrent adds of the same member, the second waits, finds the row and returns `false`.
- `SqlMembership.RemoveUnlessLast` takes an update lock on the list's `ItemGroups` row, then checks and deletes in the same transaction, so removals from one list take turns.
- `SqlItemStore.Update` works out the completion time in the statement that writes the item: `COALESCE(CompletedAt, @Now)` keeps a time that's already stored, and `OUTPUT` returns the item as stored. So editing a completed item keeps its original time, and when two members complete an item at once, the first one's time stays.
- `SqlItemGroupStore.Create` inserts the list and its first member in one transaction, so a list never exists without a member.
- `SqlItemGroupStore.UpdateSettings` compares the stored auto-delete delay as it writes (`IS DISTINCT FROM`), so only a real change restarts the countdown.
- `SqlItemGroupStore.DeleteDueForAutoDelete` is one `DELETE … OUTPUT` statement. When several app instances run auto-delete at once, each list is deleted, and so audited and announced, by one of them only.

Moving such rules into C# would mean loading, changing and saving a row, with optimistic concurrency on top: more code and more round trips for the same behavior. So the domain stays thin.

### Realtime

`ChecklistHub.JoinGroup` builds a `Caller` from the connection and asks the `SubscribeToItemGroup` use case whether it may receive the list's updates; only then does it add the connection to the SignalR group. `ChecklistNotifier` implements `IChecklistNotifier` with `IHubContext`.

### Cleanup Service

`ChecklistCleanupService` is a driving adapter too. Once a minute it creates a DI scope, which gives it a SQL connection of its own the way a request has one, and runs the `AutoDeleteItemGroups` use case. No request is behind those deletions, so the audit endpoint filter never sees them: the use case records each one through the `IAuditTrail` port, whose `AuditTrail` adapter writes to the same audit log.

### Composition Root

`Program.cs` calls `AddChecklist()` (`ChecklistServiceCollectionExtensions`), which registers every use case and binds each port to its adapter. Use cases and SQL adapters are scoped, so they share the request's connection; the notifier and the audit trail are singletons.

---

## Where the Rules Live

| Rule                                                                    | Where                                                               |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Only members may read or change a list                                  | Use cases, through `IMembership.IsMember`; the hub, likewise        |
| Whoever creates a list is its first member                              | `CreateItemGroup`; `SqlItemGroupStore.Create` (one transaction)     |
| A list always keeps at least one member                                 | `SqlMembership.RemoveUnlessLast` (locked transaction)               |
| A user is a member at most once                                         | `SqlMembership.Add` (one conditional insert)                        |
| Names aren't blank                                                      | `Name.TryCreate`, which endpoint adapters call before anything else |
| Settings stay in range                                                  | `ItemGroupSettingsInput.TryCreate`, likewise                        |
| An item added as complete counts as completed now                       | `CreateItem`, with the time from `TimeProvider`                     |
| Editing a completed item keeps its completion time; reopening clears it | `SqlItemStore.Update` (one statement)                               |
| Editing an item that's gone changes nothing and tells no one            | `UpdateItem`, when `IItemStore.Update` returns `null`               |
| Only a change to the auto-delete delay restarts its countdown           | `SqlItemGroupStore.UpdateSettings` (one statement)                  |
| Which lists auto-delete deletes, and each one once                      | `SqlItemGroupStore.DeleteDueForAutoDelete` (one statement)          |
| Every auto-delete is audited and announced to all members               | `AutoDeleteItemGroups`, through `IAuditTrail` and the notifier      |
| Who triggered a change doesn't get it back                              | `Caller.ConnectionId`, passed to the notifier by use cases          |

---

## Testing

All backend tests run against a real SQL Server (Testcontainers). There are no in-memory fakes of the ports, because they would have to copy the rules the SQL adapters enforce.

| Kind                  | Where                                              | What it covers                                                                                                                                                                                                                                                      |
| --------------------- | -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Domain tests          | `Api.Tests/Checklist/Domain/`                      | Domain values such as `Name`, without a database                                                                                                                                                                                                                    |
| Use-case tests        | `Api.Tests/Checklist/<Slice>/<Operation>.Tests.cs` | The use case built with the real SQL adapters on a `TestDatabase` connection, `CapturingNotifier` and, when the time matters, `FixedTimeProvider`: outcome, stored data, notifications                                                                              |
| SQL adapter tests     | `Api.Tests/Sql/`                                   | One port method at a time, including the rules in the table above                                                                                                                                                                                                   |
| Adapter helpers       | `Api.Tests/Http/`                                  | Small pieces of the HTTP adapter, such as `ToCaller`                                                                                                                                                                                                                |
| HTTP tests            | `*.Http.Tests.cs`                                  | The whole app through `ApiFactory`: routes, status codes, auditing, and races between concurrent requests                                                                                                                                                           |
| Architecture tests    | `Api.Tests/Architecture.Tests.cs`                  | The dependency rules: `Checklist` references nothing but .NET, use cases take only ports and `TimeProvider`, and the driving adapters (`Api.Http`, `Api.Realtime`, `Api.Background`) take no port, SQL adapter or connection, so they can only go through use cases |
| Composition root test | `Api.Tests/CompositionRoot.Tests.cs`               | The app's own container builds every use case                                                                                                                                                                                                                       |

`TestHelpers` arranges and checks data (`InsertItemGroup`, `InsertItem`, `SetItemGroupSettings`, `LoadItemGroupName`, `LoadSettings`, `LoadMembers`, `LoadItem`, `CountItems`) and makes domain values with `ValidName` and `ValidSettings`.

---

## Adding an Operation

1. Write the use case in `Checklist/UseCases/<Slice>/`, adding port methods if it needs them.
2. Implement any new port methods in the SQL adapter in `Api/Sql/`.
3. Write the endpoint adapter in `Api/Http/<Slice>/` and map it in `MapChecklistApi()`.
4. Register the use case, and any new adapter, in `AddChecklist()`.
5. Add the endpoint to the audit resource types in `AuditEndpointFilter` and to `auditedEndpoints` in `AuditLog.Http.Tests.cs` (see [audit-logs.md](audit-logs.md#adding-audit-logging-to-a-new-endpoint)).
6. Add use-case, adapter and HTTP tests.

# Backend Architecture

## Table of Contents

- [Overview](#overview)
- [Projects](#projects)
- [The Inside: Checklist](#the-inside-checklist)
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

| Folder    | Contents                                                                                     |
| --------- | -------------------------------------------------------------------------------------------- |
| `Domain/` | The records the API and SignalR send: `ItemGroup`, `ItemGroupSettings`, `Item`               |
| `Ports/`  | Interfaces the inside needs from the outside. So far `IChecklistNotifier` (realtime updates) |

The domain records keep their `[Description]` attributes (plain `System.ComponentModel`, used for the OpenAPI document) and stay the JSON and SignalR payloads. Separate HTTP DTOs only pay off once the shapes need to differ.

---

## Migration Status

The backend is moving to this layout one step at a time, and every step keeps all tests passing. Until a slice is migrated, its endpoints still follow the static `MapEndpoint` + `Execute` pattern described in [checklist.md](checklist.md#structural-conventions).

| Step                                                                                     | State       |
| ---------------------------------------------------------------------------------------- | ----------- |
| Rename `Core` to `Api` (in hexagonal terms, "core" means the inside)                    | Done        |
| `Checklist` project with the domain records and the `IChecklistNotifier` port           | Done        |
| Member slice: `Caller`, `IMembership`, use cases, endpoint adapters, hub                | Not started |
| Item slice                                                                               | Not started |
| ItemGroup slice, auto-delete and the cleanup service                                    | Not started |
| Adapters grouped by kind, old helpers removed, architecture test                        | Not started |

# Checklist Feature Specification

## Table of Contents

- [Overview](#overview)
- [Domain Model](#domain-model)
  - [ItemGroup](#itemgroup)
  - [ItemGroupSettings](#itemgroupsettings)
  - [Item](#item)
  - [Members](#members)
- [Authorization Model](#authorization-model)
- [Database Schema](#database-schema)
- [API Endpoints](#api-endpoints)
  - [Item Group Endpoints](#item-group-endpoints)
    - [GET /api/list — GetItemGroups](#get-apilist--getitemgroups)
    - [GET /api/list/{itemGroupId} — GetItemGroup](#get-apilistitemgroupid--getitemgroup)
    - [POST /api/list — CreateItemGroup](#post-apilist--createitemgroup)
    - [PUT /api/list/{itemGroupId} — UpdateItemGroup](#put-apilistitemgroupid--updateitemgroup)
    - [PUT /api/list/{itemGroupId}/settings — UpdateItemGroupSettings](#put-apilistitemgroupidsettings--updateitemgroupsettings)
    - [DELETE /api/list/{itemGroupId} — DeleteItemGroup](#delete-apilistitemgroupid--deleteitemgroup)
  - [Item Endpoints](#item-endpoints)
    - [POST /api/list/{itemGroupId} — CreateItem](#post-apilistitemgroupid--createitem)
    - [PUT /api/list/{itemGroupId}/{itemId} — UpdateItem](#put-apilistitemgroupiditemid--updateitem)
    - [DELETE /api/list/{itemGroupId}/{itemId} — DeleteItem](#delete-apilistitemgroupiditemid--deleteitem)
  - [Member Endpoints](#member-endpoints)
    - [GET /api/list/{itemGroupId}/member — GetMembers](#get-apilistitemgroupidmember--getmembers)
    - [POST /api/list/{itemGroupId}/member/{memberId} — AddMember](#post-apilistitemgroupidmembermemberid--addmember)
    - [DELETE /api/list/{itemGroupId}/member/{memberId} — RemoveMember](#delete-apilistitemgroupidmembermemberid--removemember)
- [Shared Patterns](#shared-patterns)
- [Real-Time Notifications](#real-time-notifications)
- [List Settings](#list-settings)
  - [Auto-Hide](#auto-hide)
  - [Auto-Delete](#auto-delete)
- [Structural Conventions](#structural-conventions)

---

## Overview

The Checklist feature is a collaborative list management system. Users can create item groups (shared lists), add items to them, and invite other users as members. All members of a group have equal read and write access to the group and its items.

The feature is implemented as a vertical slice under `Core/Checklist/` using ASP.NET Core Minimal APIs and Dapper for data access.

---

## Domain Model

### `ItemGroup`

Represents a shared list owned collectively by its members.

| Property   | Type                  | Description                                  |
| ---------- | --------------------- | -------------------------------------------- |
| `Id`       | `Guid`                | Unique identifier                            |
| `Name`     | `string`              | Display name. Must not be blank              |
| `Items`    | `IReadOnlyList<Item>` | Items belonging to this group                |
| `Members`  | `IReadOnlyList<Guid>` | User IDs of all members                      |
| `Settings` | `ItemGroupSettings`   | Auto-hide and auto-delete, shared by members |

`Items` and `Members` are populated contextually — see individual endpoint descriptions for what is included in each response. `Settings` is always populated.

### `ItemGroupSettings`

Per-list settings that apply to every member. Both are off (`null`) by default. See [List Settings](#list-settings).

| Property                    | Type              | Description                                                                                              |
| --------------------------- | ----------------- | -------------------------------------------------------------------------------------------------------- |
| `AutoDeleteAfterMinutes`    | `int?`            | Delete the list this many minutes after every item is done. 1440 (1 day) to 525600 (365 days), or `null` |
| `HideCompletedAfterMinutes` | `int?`            | Clients hide done items this many minutes after completion. 0 to 525600 (365 days), or `null`            |
| `AutoDeleteUpdatedAt`       | `DateTimeOffset?` | Set by the server when `AutoDeleteAfterMinutes` changes. The auto-delete countdown never starts earlier  |

### `Item`

Represents a task within an item group.

| Property      | Type              | Description                                                                   |
| ------------- | ----------------- | ----------------------------------------------------------------------------- |
| `Id`          | `Guid`            | Unique identifier                                                             |
| `Name`        | `string`          | Display name. Must not be blank                                               |
| `Description` | `string?`         | Optional longer description                                                   |
| `IsComplete`  | `bool`            | Completion flag. Defaults to `false`                                          |
| `CompletedAt` | `DateTimeOffset?` | When the item was completed. Set by the server, `null` while the item is open |
| `ItemGroupId` | `Guid`            | The group this item belongs to                                                |

### Members

Membership is a join between a user (`MemberId: Guid`) and an item group (`ItemGroupId: Guid`). There is no `Member` entity — membership is represented as a list of `Guid` on `ItemGroup` and queried directly from the `Members` table.

---

## Authorization Model

All endpoints require a valid Bearer token. Authorization is membership-based — a user must be a member of an item group to perform any operation on it or its items. There are no roles or elevated permissions; all members have equal access.

The authorization check order applied in every handler:

1. Extract `UserId` from JWT claims (`sub`, `user_id`, or `NameIdentifier`). If absent → `MissingClaim`
2. Check membership via `IsMember(itemGroupId, userId)`. If false → `Forbidden`

The exception is `CreateItemGroup`, which has no membership pre-condition — any authenticated user can create a group and becomes its first member automatically.

---

## Database Schema

Owned by `ChecklistSchemaInitializer`. Created idempotently at application startup.

```sql
CREATE TABLE ItemGroups (
    Id                        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name                      NVARCHAR(MAX)    NOT NULL,
    AutoDeleteAfterMinutes    INT              NULL,
    HideCompletedAfterMinutes INT              NULL,
    AutoDeleteUpdatedAt       DATETIMEOFFSET   NULL
);

CREATE TABLE Items (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name        NVARCHAR(MAX)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    IsComplete  BIT              NOT NULL DEFAULT 0,
    CompletedAt DATETIMEOFFSET   NULL,
    ItemGroupId UNIQUEIDENTIFIER NOT NULL,
    FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
);

CREATE TABLE Members (
    MemberId    UNIQUEIDENTIFIER NOT NULL,
    ItemGroupId UNIQUEIDENTIFIER NOT NULL,
    PRIMARY KEY (MemberId, ItemGroupId),
    FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Items_ItemGroupId                ON Items(ItemGroupId);
CREATE INDEX IX_Members_ItemGroupId              ON Members(ItemGroupId);
CREATE INDEX IX_Members_MemberId_ItemGroupId     ON Members(MemberId, ItemGroupId);
```

Deleting an `ItemGroup` cascades to both `Items` and `Members`.

Databases created before list settings existed are upgraded in place: the initializer adds the settings columns and `Items.CompletedAt` when `COL_LENGTH` reports them missing. Items that were already complete get `CompletedAt` set to the upgrade time, so nothing is hidden or deleted the moment the upgrade runs. The backfill runs through `EXEC` because SQL Server compiles a batch before running it and would reject a column added in the same batch.

The settings queries use `IS DISTINCT FROM` and `GREATEST`, which need SQL Server 2022 or later (or Azure SQL).

---

## API Endpoints

All endpoints are registered under the route group `/api/list` with `.RequireAuthorization()`.

---

### Item Group Endpoints

#### `GET /api/list` — GetItemGroups

Returns all item groups where the authenticated user is a member. Each group is populated with its **incomplete items only** (`IsComplete = 0`). `Members` is always an empty list in this response.

**Responses**

| Status             | Condition                        |
| ------------------ | -------------------------------- |
| `200 OK`           | `List<ItemGroup>` — may be empty |
| `401 Unauthorized` | Missing user ID claim            |

**Query behaviour**

Executes both queries in a single `QueryMultipleAsync` call:

1. SELECT all groups where the user is a member (JOIN on `Members`)
2. SELECT all incomplete items for those groups (JOIN on `Members` using the same `@UserId` parameter)

Items are then grouped in memory. If the first result set is empty the second result set is not consumed and an empty list is returned immediately.

**Response body example**

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Shopping",
    "items": [
      {
        "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
        "name": "Milk",
        "description": null,
        "isComplete": false,
        "completedAt": null,
        "itemGroupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
      }
    ],
    "members": [],
    "settings": {
      "autoDeleteAfterMinutes": null,
      "hideCompletedAfterMinutes": null,
      "autoDeleteUpdatedAt": null
    }
  }
]
```

---

#### `GET /api/list/{itemGroupId}` — GetItemGroup

Returns a single item group including **all items** (complete and incomplete) and the full list of member IDs.

**Route parameters**

| Parameter     | Type   | Description                |
| ------------- | ------ | -------------------------- |
| `itemGroupId` | `Guid` | The item group to retrieve |

**Responses**

| Status             | Condition                                     |
| ------------------ | --------------------------------------------- |
| `200 OK`           | `ItemGroup` with all items and all member IDs |
| `401 Unauthorized` | Missing user ID claim                         |
| `403 Forbidden`    | Authenticated user is not a member            |

**Note:** The 403 check runs before the existence check. A non-member probing for a non-existent group receives 403, not 404. This is intentional — it avoids leaking whether a group exists to non-members. Because membership is enforced by a foreign-key constraint on `Members.ItemGroupId`, a user who passes the membership check is guaranteed the group exists; the endpoint therefore never returns 404.

**Response body example**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Shopping",
  "items": [
    {
      "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "name": "Milk",
      "description": null,
      "isComplete": true,
      "completedAt": "2026-10-19T09:30:00+00:00",
      "itemGroupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "members": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"],
  "settings": {
    "autoDeleteAfterMinutes": 1440,
    "hideCompletedAfterMinutes": 60,
    "autoDeleteUpdatedAt": "2026-10-18T14:00:00+00:00"
  }
}
```

---

#### `POST /api/list` — CreateItemGroup

Creates a new item group and automatically adds the authenticated user as its first and only member. The group creation and member insertion are performed in a single database transaction.

**Request body**

| Field  | Type     | Validation                  |
| ------ | -------- | --------------------------- |
| `Name` | `string` | Required. Must not be blank |

**Responses**

| Status             | Condition                                                                                     |
| ------------------ | --------------------------------------------------------------------------------------------- |
| `201 Created`      | `ItemGroup` with `Members` containing the creator's ID. `Location` header set to `/list/{id}` |
| `400 Bad Request`  | `Name` is blank or whitespace                                                                 |
| `401 Unauthorized` | Missing user ID claim                                                                         |

**Request body example**

```json
{ "name": "Shopping" }
```

**Response body example**

Returns the created `ItemGroup`. The caller is automatically added as the first and only member. `Items` is empty on creation and both settings are off.

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Shopping",
  "items": [],
  "members": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"],
  "settings": {
    "autoDeleteAfterMinutes": null,
    "hideCompletedAfterMinutes": null,
    "autoDeleteUpdatedAt": null
  }
}
```

---

#### `PUT /api/list/{itemGroupId}` — UpdateItemGroup

Renames an existing item group.

**Route parameters**

| Parameter     | Type   | Description              |
| ------------- | ------ | ------------------------ |
| `itemGroupId` | `Guid` | The item group to update |

**Request body**

| Field  | Type     | Validation                  |
| ------ | -------- | --------------------------- |
| `Name` | `string` | Required. Must not be blank |

**Request body example**

```json
{ "name": "New name" }
```

**Responses**

| Status             | Condition                          |
| ------------------ | ---------------------------------- |
| `204 No Content`   | Update successful                  |
| `400 Bad Request`  | `Name` is blank or whitespace      |
| `401 Unauthorized` | Missing user ID claim              |
| `403 Forbidden`    | Authenticated user is not a member |

**Note:** If `itemGroupId` does not exist, the UPDATE affects zero rows and still returns `204 No Content`. There is no 404 response for this endpoint.

Settings are not part of this request; they have their own endpoint so that a rename can't switch them off.

---

#### `PUT /api/list/{itemGroupId}/settings` — UpdateItemGroupSettings

Replaces both list settings. This is a full replacement: a `null` or omitted value turns that setting off. See [List Settings](#list-settings) for what each setting does.

**Route parameters**

| Parameter     | Type   | Description              |
| ------------- | ------ | ------------------------ |
| `itemGroupId` | `Guid` | The item group to update |

**Request body**

| Field                       | Type   | Validation                                     | Default |
| --------------------------- | ------ | ---------------------------------------------- | ------- |
| `AutoDeleteAfterMinutes`    | `int?` | `null`, or 1440 (1 day) to 525600 (365 days)   | `null`  |
| `HideCompletedAfterMinutes` | `int?` | `null`, or 0 (right away) to 525600 (365 days) | `null`  |

The one-day minimum for auto-delete leaves time to undo an accidental check before the list is gone.

**Request body example**

```json
{ "autoDeleteAfterMinutes": 1440, "hideCompletedAfterMinutes": 60 }
```

**Responses**

| Status             | Condition                          |
| ------------------ | ---------------------------------- |
| `200 OK`           | The saved `ItemGroupSettings`      |
| `400 Bad Request`  | A value is outside its range       |
| `401 Unauthorized` | Missing user ID claim              |
| `403 Forbidden`    | Authenticated user is not a member |

**Response body example**

```json
{
  "autoDeleteAfterMinutes": 1440,
  "hideCompletedAfterMinutes": 60,
  "autoDeleteUpdatedAt": "2026-10-19T12:00:00+00:00"
}
```

`AutoDeleteUpdatedAt` moves to the current time only when `AutoDeleteAfterMinutes` changes (including turning it on or off), which restarts the auto-delete countdown. Changing only `HideCompletedAfterMinutes` leaves it alone. The response returns 200 with the saved settings, unlike the other `PUT` endpoints, because the client needs `AutoDeleteUpdatedAt` to show when the list will be deleted.

Validation runs before the authentication and membership checks, like the name validation on other endpoints. Members are notified with `GroupSettingsChanged`.

---

#### `DELETE /api/list/{itemGroupId}` — DeleteItemGroup

Permanently deletes an item group. All associated items and member records are removed via cascade delete.

**Route parameters**

| Parameter     | Type   | Description              |
| ------------- | ------ | ------------------------ |
| `itemGroupId` | `Guid` | The item group to delete |

**Responses**

| Status             | Condition                          |
| ------------------ | ---------------------------------- |
| `204 No Content`   | Deletion successful                |
| `401 Unauthorized` | Missing user ID claim              |
| `403 Forbidden`    | Authenticated user is not a member |

**Note:** If `itemGroupId` does not exist, the DELETE affects zero rows and still returns `204 No Content`. There is no 404 response for this endpoint.

---

### Item Endpoints

#### `POST /api/list/{itemGroupId}` — CreateItem

Creates a new item within the specified item group.

**Route parameters**

| Parameter     | Type   | Description                       |
| ------------- | ------ | --------------------------------- |
| `itemGroupId` | `Guid` | The item group to add the item to |

**Request body**

| Field         | Type      | Validation                  | Default |
| ------------- | --------- | --------------------------- | ------- |
| `Name`        | `string`  | Required. Must not be blank | —       |
| `Description` | `string?` | Optional                    | `null`  |
| `IsComplete`  | `bool`    | Optional                    | `false` |

**Responses**

| Status             | Condition                                                       |
| ------------------ | --------------------------------------------------------------- |
| `201 Created`      | `Item`. `Location` header set to `/list/{itemGroupId}/{itemId}` |
| `400 Bad Request`  | `Name` is blank or whitespace                                   |
| `401 Unauthorized` | Missing user ID claim                                           |
| `403 Forbidden`    | Authenticated user is not a member                              |

**Request body example**

```json
{ "name": "Milk", "description": "2% fat", "isComplete": false }
```

**Response body example**

```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "name": "Milk",
  "description": "2% fat",
  "isComplete": false,
  "completedAt": null,
  "itemGroupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

An item created with `isComplete: true` gets `completedAt` set to the current time.

---

#### `PUT /api/list/{itemGroupId}/{itemId}` — UpdateItem

Updates the name, description, and/or completion status of an existing item. All fields in the request body are replaced — this is a full replacement, not a patch.

**Route parameters**

| Parameter     | Type   | Description                        |
| ------------- | ------ | ---------------------------------- |
| `itemGroupId` | `Guid` | The item group the item belongs to |
| `itemId`      | `Guid` | The item to update                 |

**Request body**

| Field         | Type      | Validation                  | Default |
| ------------- | --------- | --------------------------- | ------- |
| `Name`        | `string`  | Required. Must not be blank | —       |
| `Description` | `string?` | Optional                    | `null`  |
| `IsComplete`  | `bool`    | Optional                    | `false` |

**Request body example**

```json
{ "name": "Milk", "description": "2% fat", "isComplete": true }
```

**Responses**

| Status             | Condition                          |
| ------------------ | ---------------------------------- |
| `204 No Content`   | Update successful                  |
| `400 Bad Request`  | `Name` is blank or whitespace      |
| `401 Unauthorized` | Missing user ID claim              |
| `403 Forbidden`    | Authenticated user is not a member |

**Note:** The UPDATE is scoped to both `Id = @itemId AND ItemGroupId = @itemGroupId`. If either does not exist or they do not match, zero rows are affected and `204 No Content` is still returned. There is no 404 response.

`CompletedAt` is maintained by the server and is not part of the request:

- Completing an open item sets it to the current time.
- Saving an item that is already complete (for example, to rename it) keeps the original time, so editing doesn't restart the auto-hide or auto-delete clock.
- Reopening an item clears it.

The `ItemUpdated` notification carries the stored value, read back with `OUTPUT INSERTED.CompletedAt`.

---

#### `DELETE /api/list/{itemGroupId}/{itemId}` — DeleteItem

Permanently deletes an item from an item group.

**Route parameters**

| Parameter     | Type   | Description                        |
| ------------- | ------ | ---------------------------------- |
| `itemGroupId` | `Guid` | The item group the item belongs to |
| `itemId`      | `Guid` | The item to delete                 |

**Responses**

| Status             | Condition                          |
| ------------------ | ---------------------------------- |
| `204 No Content`   | Deletion successful                |
| `401 Unauthorized` | Missing user ID claim              |
| `403 Forbidden`    | Authenticated user is not a member |

**Note:** The DELETE is scoped to both `Id = @itemId AND ItemGroupId = @itemGroupId`. Non-existent items return `204 No Content`. There is no 404 response.

---

### Member Endpoints

#### `GET /api/list/{itemGroupId}/member` — GetMembers

Returns the list of user IDs that are members of the specified item group.

**Route parameters**

| Parameter     | Type   | Description             |
| ------------- | ------ | ----------------------- |
| `itemGroupId` | `Guid` | The item group to query |

**Responses**

| Status             | Condition                          |
| ------------------ | ---------------------------------- |
| `200 OK`           | `List<Guid>` — all member IDs      |
| `401 Unauthorized` | Missing user ID claim              |
| `403 Forbidden`    | Authenticated user is not a member |

---

#### `POST /api/list/{itemGroupId}/member/{memberId}` — AddMember

Grants another user access to an item group by adding them as a member. The authenticated user must already be a member — there is no concept of an owner or admin; any member can invite others.

**Route parameters**

| Parameter     | Type   | Description                         |
| ------------- | ------ | ----------------------------------- |
| `itemGroupId` | `Guid` | The item group to add the member to |
| `memberId`    | `Guid` | The user ID to add as a member      |

**Responses**

| Status             | Condition                                   |
| ------------------ | ------------------------------------------- |
| `204 No Content`   | Member added successfully                   |
| `401 Unauthorized` | Missing user ID claim                       |
| `403 Forbidden`    | Authenticated user is not a member          |
| `409 Conflict`     | `memberId` is already a member of the group |

---

#### `DELETE /api/list/{itemGroupId}/member/{memberId}` — RemoveMember

Revokes a user's access to an item group. Any member can remove any other member. A member can remove themselves.

**Route parameters**

| Parameter     | Type   | Description                              |
| ------------- | ------ | ---------------------------------------- |
| `itemGroupId` | `Guid` | The item group to remove the member from |
| `memberId`    | `Guid` | The user ID to remove                    |

**Responses**

| Status             | Condition                                                                                                         |
| ------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `204 No Content`   | Member removed successfully                                                                                       |
| `401 Unauthorized` | Missing user ID claim                                                                                             |
| `403 Forbidden`    | Authenticated user is not a member                                                                                |
| `409 Conflict`     | `memberId` is the last remaining member of the group. Removing them would leave the group permanently unreachable |

**Orphan prevention rule:** A group must always have at least one member. Attempting to remove the last member returns `409 Conflict`. The check is performed in a single query that simultaneously verifies the group has exactly one member and that member is the target `memberId`.

---

## Shared Patterns

### Validation

Name fields (`ItemGroup.Name`, `Item.Name`) are validated with `string.IsNullOrWhiteSpace`. This check runs before the authentication and membership checks so that `400 Bad Request` is always returned for invalid input regardless of auth status.

### Idempotent Deletes and Updates

`DELETE` and `PUT` endpoints do not return `404 Not Found` if the target resource does not exist. Zero-row operations return `204 No Content`. This is intentional — it keeps the interface simple and avoids race condition handling on the client side.

### Membership Check Helper

`ChecklistConnectionExtensions` provides two shared query helpers used across all handlers:

- `IsMember(itemGroupId, userId)` — returns `false` immediately if `userId` is null, otherwise queries the `Members` table
- `IsLastMember(itemGroupId, memberId)` — single query that checks both total member count and whether the target is the sole member

---

## Real-Time Notifications

Checklist mutations are broadcast to group members in real time via SignalR.

### Architecture

- **Hub**: `ChecklistHub` at `/hubs/checklist` — authenticated, validates group membership on `JoinGroup`/`LeaveGroup`
- **Client interface**: `IChecklistClient` — typed server→client messages (`ItemCreated`, `ItemUpdated`, `ItemDeleted`, `MemberAdded`, `MemberRemoved`, `GroupRenamed`, `GroupSettingsChanged`, `GroupDeleted`)
- **Notifier abstraction**: `IChecklistNotifier` — testable wrapper around `IHubContext<ChecklistHub, IChecklistClient>`
- **Caller exclusion**: Mutations send an `X-SignalR-Connection-Id` header; the notifier uses `GroupExcept` to skip the caller. Clients learn their connection ID from the negotiate response (and a new one on every reconnect), so they must not skip negotiation

### Events

| Event                  | Payload               | Triggered by                                   |
| ---------------------- | --------------------- | ---------------------------------------------- |
| `ItemCreated`          | `(groupId, item)`     | CreateItem                                     |
| `ItemUpdated`          | `(groupId, item)`     | UpdateItem                                     |
| `ItemDeleted`          | `(groupId, itemId)`   | DeleteItem                                     |
| `MemberAdded`          | `(groupId, memberId)` | AddMember                                      |
| `MemberRemoved`        | `(groupId, memberId)` | RemoveMember                                   |
| `GroupRenamed`         | `(groupId, name)`     | UpdateItemGroup                                |
| `GroupSettingsChanged` | `(groupId, settings)` | UpdateItemGroupSettings                        |
| `GroupDeleted`         | `(groupId)`           | DeleteItemGroup, or auto-delete (no exclusion) |

---

## List Settings

Each list has two optional settings, stored on `ItemGroups` and shared by every member. Both are off by default and are changed with [UpdateItemGroupSettings](#put-apilistitemgroupidsettings--updateitemgroupsettings). Both count from `Items.CompletedAt`, which the server maintains (see [UpdateItem](#put-apilistitemgroupiditemid--updateitem)).

### Auto-Hide

`HideCompletedAfterMinutes` only changes what clients show; the server never filters items because of it, so hidden items can still be found and unchecked. A done item is hidden once `CompletedAt + HideCompletedAfterMinutes` has passed. `0` hides items as soon as they are checked. See [client.md](client.md#list-settings) for how the web client presents this.

### Auto-Delete

`ChecklistCleanupService`, a hosted `BackgroundService`, runs `AutoDeleteItemGroups` once a minute on its own `SqlConnection` (the request-scoped one isn't available outside a request). A single statement deletes every group that:

- has `AutoDeleteAfterMinutes` set,
- has at least one item, and no open items, and
- whose countdown has run out: `GREATEST(MAX(Items.CompletedAt), AutoDeleteUpdatedAt) + AutoDeleteAfterMinutes <= now`.

```sql
DELETE ig
OUTPUT DELETED.Id
FROM ItemGroups ig
CROSS APPLY (
    SELECT COUNT(*) AS ItemCount,
           COUNT(CASE WHEN i.IsComplete = 0 THEN 1 END) AS OpenCount,
           MAX(i.CompletedAt) AS LastCompletedAt
    FROM Items i WHERE i.ItemGroupId = ig.Id
) stats
WHERE ig.AutoDeleteAfterMinutes IS NOT NULL
    AND stats.ItemCount > 0 AND stats.OpenCount = 0
    AND GREATEST(stats.LastCompletedAt, ig.AutoDeleteUpdatedAt)
        <= DATEADD(MINUTE, -ig.AutoDeleteAfterMinutes, @Now);
```

Consequences of this rule:

- Unchecking an item or adding a new one cancels the countdown. Checking the last open item again starts it over.
- An empty list never counts as done, so a new list is never deleted.
- Including `AutoDeleteUpdatedAt` means turning the setting on, or changing the delay, starts the countdown from that moment. A list finished a month ago is not deleted the minute auto-delete is enabled.
- Deleting the last open item makes the list done as of its most recent completion, which may already be past the delay. The list is then deleted on the next run.
- `OUTPUT DELETED.Id` returns each deleted group exactly once, so several app instances can run the job without duplicate notifications.

For each deleted group, `AutoDeleteItemGroups` enqueues an `AutoDeleteItemGroup` audit entry (see [audit-logs.md](audit-logs.md#checklist--itemgroup)) and sends `GroupDeleted` to all members. The current time comes from the injected `TimeProvider`, and tests call `AutoDeleteItemGroups` directly with a fixed time. The test factories remove `ChecklistCleanupService`, since it would connect with the app's connection string.

---

## Structural Conventions

Each operation is a standalone `static` class following the same structure:

- `MapEndpoint(IEndpointRouteBuilder)` — registers the route with summary, description, tag, and name
- `Execute(...)` — the handler method bound by Minimal API. Contains validation, auth checks, and delegates to data methods. Handlers that participate in audit logging (`CreateItemGroup`, `CreateItem`, `AddMember`, `RemoveMember`) also accept an `AuditContext` parameter and populate the relevant ID fields before returning.
- `CreateData` / `UpdateData` / `RemoveData` / `DeleteData` / `LoadData` — `internal static` data access methods, made internal for direct testing without the HTTP stack. Delete operations use `RemoveData` (member/group/item-group handlers) or `DeleteData` (`DeleteItem`).
- `Request` record — nested inside the handler class where a request body is required
- Handlers that need the current time (`CreateItem`, `UpdateItem`, `UpdateItemGroupSettings`) take a `TimeProvider` and pass `now` into their data methods, so tests control the clock

All endpoints are registered in `ChecklistApiEndpointRouteBuilderExtension.MapChecklistApi()` on a single route group, which applies `.RequireAuthorization()` and `.AddEndpointFilter<AuditEndpointFilter>()` uniformly. The `AuditEndpointFilter` runs after every handler and enqueues an `AuditEntry` based on the HTTP outcome.

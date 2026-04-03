# Checklist Feature Specification

## Table of Contents

- [Overview](#overview)
- [Domain Model](#domain-model)
  - [ItemGroup](#itemgroup)
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
- [Structural Conventions](#structural-conventions)

---

## Overview

The Checklist feature is a collaborative list management system. Users can create item groups (shared lists), add items to them, and invite other users as members. All members of a group have equal read and write access to the group and its items.

The feature is implemented as a vertical slice under `Core/Checklist/` using ASP.NET Core Minimal APIs and Dapper for data access.

---

## Domain Model

### `ItemGroup`

Represents a shared list owned collectively by its members.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Display name. Must not be blank |
| `Items` | `IReadOnlyList<Item>` | Items belonging to this group |
| `Members` | `IReadOnlyList<Guid>` | User IDs of all members |

`Items` and `Members` are populated contextually — see individual endpoint descriptions for what is included in each response.

### `Item`

Represents a task within an item group.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Display name. Must not be blank |
| `Description` | `string?` | Optional longer description |
| `IsComplete` | `bool` | Completion flag. Defaults to `false` |
| `ItemGroupId` | `Guid` | The group this item belongs to |

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
    Id   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name NVARCHAR(MAX)    NOT NULL
);

CREATE TABLE Items (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name        NVARCHAR(MAX)    NOT NULL,
    Description NVARCHAR(MAX)    NULL,
    IsComplete  BIT              NOT NULL DEFAULT 0,
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

---

## API Endpoints

All endpoints are registered under the route group `/api/list` with `.RequireAuthorization()`.

---

### Item Group Endpoints

#### `GET /api/list` — GetItemGroups

Returns all item groups where the authenticated user is a member. Each group is populated with its **incomplete items only** (`IsComplete = 0`). `Members` is always an empty list in this response.

**Responses**

| Status | Condition |
|---|---|
| `200 OK` | `List<ItemGroup>` — may be empty |
| `401 Unauthorized` | Missing user ID claim |

**Query behaviour**

Executes two queries:
1. SELECT all groups where the user is a member
2. SELECT all incomplete items for those groups (using `IN` clause on group IDs)

Items are then grouped in memory. If the user has no groups, the second query is skipped.

---

#### `GET /api/list/{itemGroupId}` — GetItemGroup

Returns a single item group including **all items** (complete and incomplete) and the full list of member IDs.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group to retrieve |

**Responses**

| Status | Condition |
|---|---|
| `200 OK` | `ItemGroup` with all items and all member IDs |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |
| `404 Not Found` | Item group does not exist |

**Note:** The 403 check runs before the existence check. A non-member probing for a non-existent group receives 403, not 404. This is intentional — it avoids leaking whether a group exists to non-members.

---

#### `POST /api/list` — CreateItemGroup

Creates a new item group and automatically adds the authenticated user as its first and only member. The group creation and member insertion are performed in a single database transaction.

**Request body**

| Field | Type | Validation |
|---|---|---|
| `Name` | `string` | Required. Must not be blank |

**Responses**

| Status | Condition |
|---|---|
| `201 Created` | `ItemGroup` with `Members` containing the creator's ID. `Location` header set to `/list/{id}` |
| `400 Bad Request` | `Name` is blank or whitespace |
| `401 Unauthorized` | Missing user ID claim |

---

#### `PUT /api/list/{itemGroupId}` — UpdateItemGroup

Renames an existing item group.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group to update |

**Request body**

| Field | Type | Validation |
|---|---|---|
| `Name` | `string` | Required. Must not be blank |

**Responses**

| Status | Condition |
|---|---|
| `204 No Content` | Update successful |
| `400 Bad Request` | `Name` is blank or whitespace |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |

**Note:** If `itemGroupId` does not exist, the UPDATE affects zero rows and still returns `204 No Content`. There is no 404 response for this endpoint.

---

#### `DELETE /api/list/{itemGroupId}` — DeleteItemGroup

Permanently deletes an item group. All associated items and member records are removed via cascade delete.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group to delete |

**Responses**

| Status | Condition |
|---|---|
| `204 No Content` | Deletion successful |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |

**Note:** If `itemGroupId` does not exist, the DELETE affects zero rows and still returns `204 No Content`. There is no 404 response for this endpoint.

---

### Item Endpoints

#### `POST /api/list/{itemGroupId}` — CreateItem

Creates a new item within the specified item group.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group to add the item to |

**Request body**

| Field | Type | Validation | Default |
|---|---|---|---|
| `Name` | `string` | Required. Must not be blank | — |
| `Description` | `string?` | Optional | `null` |
| `IsComplete` | `bool` | Optional | `false` |

**Responses**

| Status | Condition |
|---|---|
| `201 Created` | `Item`. `Location` header set to `/list/{itemGroupId}/{itemId}` |
| `400 Bad Request` | `Name` is blank or whitespace |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |

---

#### `PUT /api/list/{itemGroupId}/{itemId}` — UpdateItem

Updates the name, description, and/or completion status of an existing item. All fields in the request body are replaced — this is a full replacement, not a patch.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group the item belongs to |
| `itemId` | `Guid` | The item to update |

**Request body**

| Field | Type | Validation | Default |
|---|---|---|---|
| `Name` | `string` | Required. Must not be blank | — |
| `Description` | `string?` | Optional | `null` |
| `IsComplete` | `bool` | Optional | `false` |

**Responses**

| Status | Condition |
|---|---|
| `204 No Content` | Update successful |
| `400 Bad Request` | `Name` is blank or whitespace |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |

**Note:** The UPDATE is scoped to both `Id = @itemId AND ItemGroupId = @itemGroupId`. If either does not exist or they do not match, zero rows are affected and `204 No Content` is still returned. There is no 404 response.

---

#### `DELETE /api/list/{itemGroupId}/{itemId}` — DeleteItem

Permanently deletes an item from an item group.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group the item belongs to |
| `itemId` | `Guid` | The item to delete |

**Responses**

| Status | Condition |
|---|---|
| `204 No Content` | Deletion successful |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |

**Note:** The DELETE is scoped to both `Id = @itemId AND ItemGroupId = @itemGroupId`. Non-existent items return `204 No Content`. There is no 404 response.

---

### Member Endpoints

#### `GET /api/list/{itemGroupId}/member` — GetMembers

Returns the list of user IDs that are members of the specified item group.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group to query |

**Responses**

| Status | Condition |
|---|---|
| `200 OK` | `List<Guid>` — all member IDs |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |

---

#### `POST /api/list/{itemGroupId}/member/{memberId}` — AddMember

Grants another user access to an item group by adding them as a member. The authenticated user must already be a member — there is no concept of an owner or admin; any member can invite others.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group to add the member to |
| `memberId` | `Guid` | The user ID to add as a member |

**Responses**

| Status | Condition |
|---|---|
| `204 No Content` | Member added successfully |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |
| `409 Conflict` | `memberId` is already a member of the group |

---

#### `DELETE /api/list/{itemGroupId}/member/{memberId}` — RemoveMember

Revokes a user's access to an item group. Any member can remove any other member. A member can remove themselves.

**Route parameters**

| Parameter | Type | Description |
|---|---|---|
| `itemGroupId` | `Guid` | The item group to remove the member from |
| `memberId` | `Guid` | The user ID to remove |

**Responses**

| Status | Condition |
|---|---|
| `204 No Content` | Member removed successfully |
| `401 Unauthorized` | Missing user ID claim |
| `403 Forbidden` | Authenticated user is not a member |
| `409 Conflict` | `memberId` is the last remaining member of the group. Removing them would leave the group permanently unreachable |

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

## Structural Conventions

Each operation is a standalone `static` class following the same structure:

- `MapEndpoint(IEndpointRouteBuilder)` — registers the route with summary, description, tag, and name
- `Execute(...)` — the handler method bound by Minimal API. Contains validation, auth checks, and delegates to data methods
- `CreateData` / `UpdateData` / `RemoveData` / `LoadData` — `internal static` data access methods, made internal for direct testing without the HTTP stack
- `Request` record — nested inside the handler class where a request body is required

All endpoints are registered in `ChecklistApiEndpointRouteBuilderExtension.MapChecklistApi()` on a single route group, which applies `.RequireAuthorization()` uniformly.

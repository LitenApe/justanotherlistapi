# Accounts Specification

## Table of Contents

- [Overview](#overview)
- [Rules](#rules)
- [Signing Up](#signing-up)
- [Signing In](#signing-in)
- [Tokens](#tokens)
- [Rate Limit](#rate-limit)
- [Endpoints](#endpoints)
  - [POST /api/auth/sign-up — SignUp](#post-apiauthsign-up--signup)
  - [POST /api/auth/sign-in — SignIn](#post-apiauthsign-in--signin)
- [Database Schema](#database-schema)
- [Where It Lives](#where-it-lives)

---

## Overview

People have accounts: a name, an email address and a password. The API keeps them itself, with ASP.NET Core Identity on SQL Server, and signs people in with tokens it issues. The email address identifies the account: people sign in with it, and nothing else about them is unique. Names don't have to be unique.

Email addresses aren't confirmed: an account works as soon as it's created.

---

## Rules

Each value is parsed into a domain value before anything else happens, so a request that breaks a rule gets `400 Bad Request` and changes nothing.

| Value    | Domain value   | Rule                                                                                                                               |
| -------- | -------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Name     | `DisplayName`  | Trimmed, then 1 to 50 characters                                                                                                   |
| Email    | `EmailAddress` | Trimmed; at most 254 characters; exactly one `@`, with text on both sides; no whitespace. Accounts are looked up ignoring case     |
| Password | `Password`     | 12 to 128 characters, kept exactly as typed. No rules about kinds of characters: length is what makes a password hard to guess (NIST SP 800-63B) |

One account per email address, ignoring case: `Kim@example.com` and `kim@EXAMPLE.com` are the same account.

Passwords are stored only as Identity's hash (PBKDF2). They are never logged, audited or sent back.

---

## Signing Up

Signing up creates the account and signs its owner in, in one step: the response carries a token. When another account already uses the email address, ignoring case, nothing is created and the answer is `409 Conflict`.

Two sign-ups with one email address at the same moment can both pass Identity's check for a duplicate. The unique index on `Users.NormalizedEmail` decides: one gets the account, the other `409`.

## Signing In

Signing in takes the email address and the password, and answers with a token when they match. A wrong password and an email address without an account get the same answer, `401 Unauthorized`, so the response never says which was wrong.

**Lockout.** Five wrong passwords in a row lock the account for five minutes. While it's locked, every password is refused with `423 Locked`, the right one too. A right password starts the count again. Wrong passwords tried in parallel all count: the store adds each one in the database, not in the copy of the account a request loaded.

## Tokens

The tokens are JWTs signed with HMAC-SHA256, issued by `JwtTokenIssuer`:

| Claim | Value                                                                        |
| ----- | ---------------------------------------------------------------------------- |
| `sub` | The user's ID, which lists use to name their members                         |
| `iss` | `Auth:Issuer`, by default `JustAnotherList`                                  |
| `aud` | `Auth:Audience`, by default `JustAnotherList`                                |
| `exp` | `Auth:TokenLifetime` after issuing, by default 8 hours, in whole seconds     |

The response's `expiresAt` is the token's `exp`.

`Auth:SigningKey` signs every token: a base64-encoded secret of at least 32 random bytes. The app refuses to start without one. `appsettings.Development.json` holds a key for local development only; every other environment must set its own secret, since anyone who has the key can sign in as anyone.

Until the client signs in with these tokens, requests to `/api/list` are still checked against the OAuth authority described in [authentication.md](authentication.md).

## Rate Limit

The account endpoints need no token, since they're how people get one, so each client may only call them so often: 20 requests a minute per IP address, shared by all of them, in a fixed window. Requests over the limit get `429 Too Many Requests` without reaching the endpoint, and the audit log records them with the outcome `TooManyRequests` (see [audit-logs.md](audit-logs.md)).

`Auth:RateLimit:PermitLimit` changes the limit. The tests raise it, since all their requests come from one address.

---

## Endpoints

All account endpoints live under `/api/auth`, need no token, and are audited with the resource type `'Account'`.

A session, the answer to signing up or signing in:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs…",
  "expiresAt": "2026-11-21T17:30:15+00:00",
  "user": {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "Kim",
    "email": "kim@example.com"
  }
}
```

#### `POST /api/auth/sign-up` — SignUp

```json
{ "name": "Kim", "email": "kim@example.com", "password": "correct horse battery" }
```

| Status            | When                                                                                                         |
| ----------------- | ------------------------------------------------------------------------------------------------------------ |
| `201 Created`     | The account is created; the body is the session                                                              |
| `400 Bad Request` | A validation problem whose `errors` name each field that breaks its rule: `name`, `email` and/or `password` |
| `409 Conflict`    | Another account uses the email address, ignoring case                                                        |
| `429`             | Over the [rate limit](#rate-limit)                                                                           |

The audit entry's `TargetUserId` is the new account, on success.

#### `POST /api/auth/sign-in` — SignIn

```json
{ "email": "kim@example.com", "password": "correct horse battery" }
```

| Status             | When                                                                                   |
| ------------------ | -------------------------------------------------------------------------------------- |
| `200 OK`           | The password matches; the body is the session                                          |
| `400 Bad Request`  | The email address isn't one, or the password is missing                                |
| `401 Unauthorized` | No account uses the email address, or the password is wrong. Audited as `InvalidCredentials` |
| `423 Locked`       | The account is locked out after five wrong passwords in a row                          |
| `429`              | Over the [rate limit](#rate-limit)                                                     |

The audit entry's `TargetUserId` is the account, on success.

---

## Database Schema

```sql
CREATE TABLE Users (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name              NVARCHAR(50)     NOT NULL,
    Email             NVARCHAR(254)    NOT NULL,
    NormalizedEmail   NVARCHAR(254)    NOT NULL,  -- upper-cased, for lookups that ignore case
    PasswordHash      NVARCHAR(MAX)    NOT NULL,
    SecurityStamp     NVARCHAR(64)     NOT NULL,  -- changes with the password
    LockoutEnd        DATETIMEOFFSET   NULL,      -- until when every password is refused
    AccessFailedCount INT              NOT NULL DEFAULT 0,
    CreatedAt         DATETIMEOFFSET   NOT NULL
);

CREATE UNIQUE INDEX UX_Users_NormalizedEmail ON Users(NormalizedEmail);
```

`ChecklistSchemaInitializer` creates the table and the index when they're missing, so databases created before accounts existed get them too.

---

## Where It Lives

| Piece                                      | Where                                                                                                   |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| The rules for names, emails and passwords  | `DisplayName`, `EmailAddress`, `Password` in `Checklist/Domain/`                                         |
| Signing up and signing in                  | The `SignUp` and `SignIn` use cases in `Checklist/UseCases/Account/`, through the `IAccounts` and `ITokenIssuer` ports |
| Hashing, checking and lockout              | `IdentityAccounts` (ASP.NET Core Identity), set up in `AddAccounts()`                                    |
| Keeping accounts                           | `SqlAccountStore`, Identity's store interfaces on `Users`                                               |
| Tokens                                     | `JwtTokenIssuer`, with its settings in `AuthOptions` (`Auth` configuration section)                     |
| The endpoints and the rate limit           | `Api/Http/Account/`, mapped by `MapAccountApi()`; `AuthRateLimit`                                       |

See [architecture.md](architecture.md) for how the pieces fit the ports and adapters.

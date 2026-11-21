# Authentication Specification

## Table of Contents

- [Overview](#overview)
- [Current Implementation](#current-implementation)
- [How A User Authenticates Against The API](#how-a-user-authenticates-against-the-api)
- [Token Validation Behavior](#token-validation-behavior)
- [Identity Resolution In Handlers](#identity-resolution-in-handlers)
- [Authorization Boundaries](#authorization-boundaries)
- [Failure Modes And Status Codes](#failure-modes-and-status-codes)
- [OpenAPI And Scalar Behavior](#openapi-and-scalar-behavior)
- [Configuration Keys](#configuration-keys)
- [Out Of Scope](#out-of-scope)

---

## Overview

This document describes how authentication works in the codebase and how users authenticate when calling the application.

The API keeps its own accounts and issues its own tokens: people sign up or sign in at `/api/auth` (see [accounts.md](accounts.md)) and get a JWT signed with the API's secret key. Every other endpoint accepts those tokens, and no others, through ASP.NET Core JwtBearer authentication.

---

## Current Implementation

Authentication and authorization are configured in `Api/Program.cs`:

- `AddAuthentication().AddJwtBearer()` is registered, and its options are configured from `AuthOptions` (the `Auth` configuration section).
- `AddAuthorization()` is registered.
- Middleware order is:
  1. `UseRateLimiter()`
  2. `UseAuthentication()`
  3. `UseAuthorization()`
- Checklist routes are mapped under `/api/list` and the route group has `.RequireAuthorization()`.
- Account routes are mapped under `/api/auth` and the route group has `.AllowAnonymous()` and the account rate limit.

Effectively, every endpoint except sign-up and sign-in requires a Bearer token the API issued.

---

## How A User Authenticates Against The API

A user signs up (`POST /api/auth/sign-up`) or signs in (`POST /api/auth/sign-in`) with their email address and password. Both answer with a session:

```json
{ "accessToken": "eyJhbGciOiJIUzI1NiIs…", "expiresAt": "2026-11-21T17:30:15+00:00", "user": { "id": "…", "name": "Kim", "email": "kim@example.com" } }
```

The token goes in the `Authorization` header of every other request:

```http
Authorization: Bearer <accessToken>
```

The SignalR hub can't set headers on a WebSocket, so it also accepts the token as an `access_token` query parameter on requests under `/hubs` (`OnMessageReceived`).

### Local development (Aspire setup)

Under Aspire, Api runs in Development and signs tokens with the development key in `Api/appsettings.Development.json`. The client signs people in through its own sign-up and sign-in pages. To call the API by hand, sign in and copy the `accessToken`:

```http
POST http://localhost:5093/api/auth/sign-in
Content-Type: application/json

{ "email": "kim@example.com", "password": "correct horse battery" }
```

---

## Token Validation Behavior

JwtBearer checks each token against the same settings `JwtTokenIssuer` signs with:

- **Signature:** HMAC-SHA256 with `Auth:SigningKey`. Tokens signed with any other key or algorithm are refused.
- **Issuer and audience:** `Auth:Issuer` and `Auth:Audience`, both `JustAnotherList` by default.
- **Lifetime:** the token must be within its `nbf` and `exp`, with 30 seconds of clock skew. Tokens last `Auth:TokenLifetime`, 8 hours by default, and there are no refresh tokens: after that, people sign in again.

Authentication failures trigger `OnAuthenticationFailed`, which enqueues an audit entry with operation/outcome `AuthenticationFailed`.

JWTs are stateless, so signing out only forgets the token in the browser, and resetting a password doesn't end the tokens already issued: they keep working until they expire.

---

## Identity Resolution In Handlers

After a token is accepted by JwtBearer middleware, the driving adapters resolve the caller identity via `ClaimsPrincipal.GetUserId()` in `Api/Extensions/ClaimsPrincipalExtension.cs`: endpoint adapters through `ToCaller()` (`Api/Http/CallerExtensions.cs`), which also reads the `X-SignalR-Connection-Id` header into a `Caller`, and `ChecklistHub` directly.

Claim lookup order:

1. `ClaimTypes.NameIdentifier` (JwtBearer maps the token's `sub` to it)
2. `sub`
3. `user_id`

The value must parse as a `Guid`. The API's own tokens always carry the account's ID in `sub`.

- If claim is missing: returns `null`
- If claim is present but not a valid `Guid`: returns `null`

Endpoint adapters treat `null` as unauthorized and return `401 Unauthorized`; the hub refuses `JoinGroup`.

---

## Authorization Boundaries

Authentication and authorization are separate checks in request flow:

1. Authentication (JwtBearer): token validity.
2. Authorization (`RequireAuthorization`): endpoint requires authenticated principal.
3. Access checks in the use cases: group membership through the `IMembership` port for group, item and member operations (see [architecture.md](architecture.md)).

Typical result:

- Authenticated but not a member of the target group -> `403 Forbidden`
- Authenticated and member -> the use case continues

---

## Failure Modes And Status Codes

Current behavior in this codebase:

- `401 Unauthorized`
  - Missing/invalid Bearer token (authentication middleware)
  - Valid authenticated principal but missing/invalid GUID user identifier claim (endpoint check via `ToCaller()`)
  - From `POST /api/auth/sign-in`: wrong email or password
- `403 Forbidden`
  - Authenticated caller is not a member of the requested group
- `423 Locked`
  - From `POST /api/auth/sign-in`: five wrong passwords in a row locked the account for five minutes
- `429 Too Many Requests`
  - Over the account endpoints' rate limit

Audit logging distinguishes JWT validation failure (`AuthenticationFailed`), the endpoint-level missing claim (`MissingClaim`) and a failed sign-in (`InvalidCredentials`).

---

## OpenAPI And Scalar Behavior

OpenAPI document generation adds a `Bearer` security scheme through `BearerSecuritySchemeTransformer`, with a global security requirement that references it. Scalar prefers that scheme: sign in, then paste the `accessToken` the response carries.

---

## Configuration Keys

Authentication-related keys used by the application:

- `Auth:SigningKey` (`Auth__SigningKey`): required. A base64-encoded secret of at least 32 random bytes that signs and checks every token; the app refuses to start without it. `appsettings.Development.json` holds one for local development only. Every other environment must set its own secret: anyone who has it can sign in as anyone.
- `Auth:Issuer` and `Auth:Audience`: the tokens' `iss` and `aud` claims, both `JustAnotherList` by default.
- `Auth:TokenLifetime`: how long a token works, 8 hours (`08:00:00`) by default.
- `Auth:RateLimit:PermitLimit`: requests per client per minute to the account endpoints, 20 by default.

---

## Out Of Scope

Not implemented in this repository:

- Refresh tokens, or staying signed in across tabs
- Ending tokens before they expire (on sign-out or a password reset)
- Confirming email addresses
- Signing in through another identity provider

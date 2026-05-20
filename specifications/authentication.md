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

This document describes how authentication currently works in the codebase and how users authenticate when calling the application.

The API does not issue tokens itself. It accepts JWT Bearer access tokens and validates them through ASP.NET Core JwtBearer authentication.

---

## Current Implementation

Authentication and authorization are configured in `Core/Program.cs`:

- `AddAuthentication().AddJwtBearer(...)` is registered.
- `AddAuthorization()` is registered.
- Middleware order is:
  1. `UseAuthentication()`
  2. `UseAuthorization()`
- Checklist routes are mapped under `/api/list` and the route group has `.RequireAuthorization()`.

Effectively, all checklist endpoints require an authenticated Bearer token.

---

## How A User Authenticates Against The API

A user authenticates by obtaining an access token from an OAuth provider and sending it as a Bearer token in the `Authorization` header.

Example request header:

```http
Authorization: Bearer <access_token>
```

### Local development (Aspire setup)

When running through Aspire, the app wires `OAuth__Authority` to the mock OAuth server:

- Authority base: `http://<oauth-host>:8080/default`
- The README documents this manual token endpoint for client credentials:
  - `POST http://localhost:8080/default/token`

The Scalar UI is also configured for local token retrieval with:

- `client_id`: `00000000-0000-0000-0000-000000000001`
- `client_secret`: `dev`

### Non-Aspire / external provider

The application expects OAuth provider details via configuration (`OAuth:Authority`, optional `OAuth:Audience`).

No login endpoint, token exchange endpoint, or credential validation endpoint is implemented by this API.

---

## Token Validation Behavior

JwtBearer options are configured as follows:

- `OAuth:Authority`
  - If present, assigned to `options.Authority`.
  - `RequireHttpsMetadata` is set to `false` only when authority starts with `http://`.
  - Otherwise `RequireHttpsMetadata` remains `true`.
- `OAuth:Audience`
  - If present, assigned to `options.Audience`.
  - If absent, `options.TokenValidationParameters.ValidateAudience = false`.

Authentication failures trigger `OnAuthenticationFailed`, which enqueues an audit entry with operation/outcome `AuthenticationFailed`.

---

## Identity Resolution In Handlers

After a token is accepted by JwtBearer middleware, handlers resolve the caller identity via `ClaimsPrincipal.GetUserId()` in `Core/Extensions/ClaimsPrincipalExtension.cs`.

Claim lookup order:

1. `ClaimTypes.NameIdentifier`
2. `sub`
3. `user_id`

The value must parse as a `Guid`.

- If claim is missing: returns `null`
- If claim is present but not a valid `Guid`: returns `null`

Handlers treat `null` as unauthorized and return `401 Unauthorized`.

---

## Authorization Boundaries

Authentication and authorization are separate checks in request flow:

1. Authentication (JwtBearer): token validity.
2. Authorization (`RequireAuthorization`): endpoint requires authenticated principal.
3. Domain access checks in handlers: group membership (`IsMember(...)`) for group/item/member operations.

Typical result:

- Authenticated but not a member of the target group -> `403 Forbidden`
- Authenticated and member -> handler continues

---

## Failure Modes And Status Codes

Current behavior in this codebase:

- `401 Unauthorized`
  - Missing/invalid Bearer token (authentication middleware)
  - Valid authenticated principal but missing/invalid GUID user identifier claim (handler check via `GetUserId()`)
- `403 Forbidden`
  - Authenticated caller is not a member of the requested group

Audit logging distinguishes JWT validation failure (`AuthenticationFailed`) from handler-level missing claim (`MissingClaim` outcome mapping in the audit feature).

---

## OpenAPI And Scalar Behavior

OpenAPI document generation adds security schemes through `BearerSecuritySchemeTransformer`:

- Always adds `Bearer` scheme if the authentication scheme exists.
- Adds `OAuth2` client-credentials scheme only when `OAuth:Authority` is configured.
- Adds a global security requirement referencing `Bearer`.

Scalar UI behavior:

- If `OAuth:Authority` exists, Scalar prefers `OAuth2` and exposes a client-credentials flow.
- If not, Scalar prefers `Bearer`.

---

## Configuration Keys

Authentication-related keys currently used by the application:

- `OAuth:Authority` (or environment variable `OAuth__Authority`)
- `OAuth:Audience` (or environment variable `OAuth__Audience`)

---

## Out Of Scope

Not implemented in this repository:

- User registration
- Username/password login UI or endpoint
- Token minting/issuing service
- Refresh token flow

These are delegated to an external OAuth/OIDC provider.

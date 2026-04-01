# JustAnotherList

JustAnotherList is an open-source, minimalist app for organising life into shareable checklists. Create unlimited lists, invite collaborators, and track items — all through a clean REST API.

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| Backend | ASP.NET Core 10 (Minimal API) |
| Data access | Dapper + SQL Server |
| Authentication | JWT Bearer — delegated to an external OAuth 2.0 / OIDC provider |
| API docs | Scalar (OpenAPI) |
| Observability | OpenTelemetry (traces + logs, OTLP export) |
| Dev orchestration | .NET Aspire |

## Project structure

```
Aspire/          - .NET Aspire AppHost — wires all services for local development
Core/            - ASP.NET Core backend
  Checklist/     - All API endpoint handlers (Item, ItemGroup, Member)
  Utility/       - OpenAPI transformer
  DatabaseInitializer.cs  - Runs idempotent CREATE TABLE statements at startup
  Program.cs     - Application composition root
Core.Tests/      - xUnit unit tests (SQLite in-memory, no running server needed)
```

## API endpoints

All endpoints live under `/api/list` and require a valid Bearer token.

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/list` | Get all item groups for the authenticated user |
| `POST` | `/api/list` | Create a new item group |
| `GET` | `/api/list/{id}` | Get a single item group with all its items and members |
| `PUT` | `/api/list/{id}` | Rename an item group |
| `DELETE` | `/api/list/{id}` | Delete an item group |
| `POST` | `/api/list/{id}` | Create an item in an item group |
| `PUT` | `/api/list/{groupId}/{itemId}` | Update an item |
| `DELETE` | `/api/list/{groupId}/{itemId}` | Delete an item |
| `GET` | `/api/list/{id}/member` | List member IDs of an item group |
| `POST` | `/api/list/{id}/member/{memberId}` | Add a member to an item group |
| `DELETE` | `/api/list/{id}/member/{memberId}` | Remove a member from an item group |

Interactive docs (Scalar UI) are available at `/scalar/v1` when the app is running.

## Authentication

The API validates JWT Bearer tokens issued by an external OAuth 2.0 / OIDC provider. Configure the provider's issuer URL via:

```json
{
  "OAuth": {
    "Authority": "https://your-provider.com/tenant"
  }
}
```

The authority is passed to `AddJwtBearer` at startup. `RequireHttpsMetadata` is automatically disabled when the authority URL starts with `http://` to support the local mock server.

### User identity

Every protected operation reads the caller's identity from the `sub` claim of the incoming JWT. This claim must be a valid `Guid`. All other claim shapes are ignored.

## Database

The schema is managed without a migration framework. On each startup, `DatabaseInitializer` runs three idempotent `CREATE TABLE IF NOT EXISTS` statements against SQL Server:

| Table | Purpose |
|---|---|
| `ItemGroups` | A named list owned by one or more members |
| `Items` | Individual checklist entries belonging to an item group |
| `Members` | Join table linking users (by `Guid` identity) to item groups |

Because the initializer is idempotent it is safe to run against an existing database and requires no CLI tooling to manage schema changes. To update the schema, modify `Core/DatabaseInitializer.cs` directly.

## Requirements

- .NET SDK 10
- Docker (or another OCI-compatible runtime)

Docker is required because Aspire spins up SQL Server and the OAuth mock server as persistent containers at startup.

## Quickstart (local development)

1. Clone the repo:

   ```bash
   git clone <repo-url>
   cd justanotherlistapi
   ```

2. Start the Aspire host:

   ```bash
   dotnet run --project Aspire
   ```

   Aspire will start and health-check the following containers before launching the backend:
   - **SQL Server** (persistent volume — data survives restarts)
   - **mock-oauth2-server** (`ghcr.io/navikt/mock-oauth2-server:2.1.10`) on port `8080`

   The backend starts once all dependencies are healthy.

## Getting a token locally

When the app is running with Aspire, the Scalar UI at `/scalar/v1` is pre-configured with `client_id: dev` and `client_secret: dev`. Click **Authorize → Get Token** in Scalar to fetch a token directly from the mock server — no copy-pasting required.

Alternatively, fetch a token manually:

```bash
curl -X POST http://localhost:8080/default/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=00000000-0000-0000-0000-000000000001&client_secret=dev"
```

Copy the returned `access_token` and supply it as a `Bearer` token in any HTTP client. The `sub` claim in the token will be used as the authenticated user's ID — make sure it is a valid `Guid` (the mock server generates one by default).

## Running without Aspire (alternative setup)

If you prefer a local SQL Server install or an external endpoint, set the connection string directly:

```json
{
  "ConnectionStrings": {
    "database": "Server=localhost,1433;Database=JustAnotherListDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
  },
  "OAuth": {
    "Authority": "https://your-provider.com/tenant"
  }
}
```

Then run the backend directly:

```bash
dotnet run --project Core
```

The database tables will be created automatically on first startup.

## Tests

Backend unit tests use xUnit and an in-memory SQLite database — no running SQL Server or OAuth server is needed.

```bash
dotnet test
```

The test project (`Core.Tests`) references `Core` internals via `InternalsVisibleTo` so individual handler methods can be tested directly without spinning up an HTTP server.

## Contributing

- Author / maintainer: Son Thanh Vo
- Contributions and forks are welcome. Open issues or PRs for changes.


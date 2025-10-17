# JustAnotherList

JustAnotherList is an open-source, minimalist app for organizing life into shareable lists. From last minute grocery runs to longterm project checklists. Create unlimited lists, invite friends or family to collaborate, and keep everything you need in one simple, fast interface. Built to be easy to extend, it pairs an ASP.NET Core + EF Core backend with a Next.js frontend so you can focus on features, not plumbing.

## Key features

- Create and manage unlimited shopping lists and items.
- Share lists and invite collaborators (membership endpoints).
- Clean API surface with EF Core migrations and a reactive Next.js UI.
- Designed to be easy to extend and deploy.

## Requirements

- .NET SDK 9
- Node.js 24
- Docker/Rancher

### Why Docker (or another container/runtime)?

The Aspire host used for development creates and migrates a persistent Microsoft SQL Server instance at startup. The backend expects a reachable SQL Server when it starts. Docker provides the simplest, reproducible way to run a disposable SQL Server host with a persistent volume for development. If you prefer, you can run a local SQL Server Developer/Express install or point the app to an external SQL endpoint by updating configuration.

Dev Docker example (MSSQL 2022):

```bash
docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=Your_password123' \
  -p 1433:1433 --name justanotherlist-db \
  -v justanotherlist-mssql-data:/var/opt/mssql -d mcr.microsoft.com/mssql/server:2022-latest
```

Example connection string (appsettings.json key: `ConnectionStrings:database`):

```json
{
  "ConnectionStrings": {
    "database": "Server=localhost,1433;Database=JustAnotherListDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
  }
}
```

Note: use a strong SA password for local development.

## Quickstart (development)

1. Clone the repo:

   ```bash
   git clone <repo-url>
   cd JustAnotherListApi
   ```

2. Start the distributed development host (recommended):

   ```bash
   dotnet run --project Aspire
   ```

   Aspire will ensure the configured SQL Server is reachable, apply EF Core migrations, then start the backend and frontend dev workflow.

3. Optional: run the frontend only:
   ```bash
   cd Client
   npm install
   npm run dev
   ```
   The client dev server runs on http://localhost:3000 by default.

## Database & migrations

- Migrations live in `Core/Migrations`. Aspire applies migrations at startup. To run migrations manually, use the EF Core CLI targeted at the `Core` project.

## Tests

- Run backend tests with:
  ```bash
  dotnet test
  ```
- The client currently has no tests.

## Running notes

- Development: start the `Aspire` project as described above. There is no production-ready run configuration yet.
- The Aspire host wires the SQL database, Core backend and Client app at runtime (see `Aspire/Program.cs`).

## Contributing & Maintainer

- Author / maintainer: Son Thanh Vo
- Contributions and forks are welcome. Open issues or PRs for changes.

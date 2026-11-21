using Aspire.Hosting.Dotnet;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<SqlServerServerResource> sql = builder
    .AddSqlServer("SqlServer")
    .WithImageTag("2025-latest")
    .WithLifetime(ContainerLifetime.Persistent);
IResourceBuilder<SqlServerDatabaseResource> db = sql.AddDatabase("database");

// The path is relative to this AppHost's folder. Api signs people in itself (accounts.md),
// with the development signing key from its appsettings.Development.json.
IResourceBuilder<DotnetProjectResource> api = builder
    .AddDotnetProject("Api", "../Api/Api.csproj")
    .WaitFor(db)
    .WithReference(db);

// The Vite dev server proxies /api and /hubs to Api.
// A fixed port keeps the origin stable, so browser storage survives restarts.
builder
    .AddViteApp("client", "../Client")
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();

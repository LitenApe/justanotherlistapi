using Aspire.Hosting.Dotnet;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<SqlServerServerResource> sql = builder
    .AddSqlServer("SqlServer")
    .WithImageTag("2025-latest")
    .WithLifetime(ContainerLifetime.Persistent);
IResourceBuilder<SqlServerDatabaseResource> db = sql.AddDatabase("database");

// Catches the emails Api sends, such as password reset links; open its inbox from the dashboard.
IResourceBuilder<ContainerResource> mail = builder
    .AddContainer("mail", "axllent/mailpit", "v1.34.0")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithHttpHealthCheck("/readyz");
EndpointReference smtp = mail.GetEndpoint("smtp");

// The path is relative to this AppHost's folder. Api signs people in itself (accounts.md),
// with the development signing key from its appsettings.Development.json.
IResourceBuilder<DotnetProjectResource> api = builder
    .AddDotnetProject("Api", "../Api/Api.csproj")
    .WaitFor(db)
    .WithReference(db)
    .WaitFor(mail)
    .WithEnvironment("Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", smtp.Property(EndpointProperty.Port));

// The Vite dev server proxies /api and /hubs to Api.
// A fixed port keeps the origin stable, so browser storage survives restarts.
var client = builder
    .AddViteApp("client", "../Client")
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithReference(api)
    .WaitFor(api);

// The links in Api's emails open the client.
api.WithEnvironment("App__ClientUrl", client.GetEndpoint("http"));

builder.Build().Run();

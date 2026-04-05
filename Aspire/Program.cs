using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<SqlServerServerResource> sql = builder
    .AddSqlServer("SqlServer")
    .WithLifetime(ContainerLifetime.Persistent);
IResourceBuilder<SqlServerDatabaseResource> db = sql.AddDatabase("database");

IResourceBuilder<ContainerResource> oauth = builder
    .AddContainer("oauth", "ghcr.io/navikt/mock-oauth2-server", "2.1.10")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/default/.well-known/openid-configuration")
    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<ProjectResource> core = builder
    .AddProject<Core>("Core")
    .WaitFor(db)
    .WaitFor(oauth)
    .WithReference(db)
    .WithEnvironment(
        "OAuth__Authority",
        ReferenceExpression.Create($"{oauth.GetEndpoint("http")}/default")
    );

builder.Build().Run();

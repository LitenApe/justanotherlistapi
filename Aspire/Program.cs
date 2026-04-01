using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("SqlServer").WithLifetime(ContainerLifetime.Persistent);
var db = sql.AddDatabase("database");

var oauth = builder
    .AddContainer("oauth", "ghcr.io/navikt/mock-oauth2-server", "2.1.10")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithHttpHealthCheck("/default/.well-known/openid-configuration")
    .WithLifetime(ContainerLifetime.Persistent);

var core = builder
    .AddProject<Core>("Core")
    .WaitFor(db)
    .WaitFor(oauth)
    .WithReference(db)
    .WithEnvironment(
        "OAuth__Authority",
        ReferenceExpression.Create($"{oauth.GetEndpoint("http")}/default")
    );

builder.Build().Run();

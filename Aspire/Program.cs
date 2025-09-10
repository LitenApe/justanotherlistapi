using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("SqlServer")
                    .WithLifetime(ContainerLifetime.Persistent);
var db = sql.AddDatabase("database");

var core = builder.AddProject<Core>("Core")
                    .WaitFor(db)
                    .WithReference(db);

var client = builder.AddNpmApp("Client", "../Client", "dev")
    .WaitFor(core)
    .WithReference(core)
    .WithUrl("http://localhost:5173");

builder.Build().Run();

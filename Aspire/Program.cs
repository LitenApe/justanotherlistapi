using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("SqlServer")
                    .WithLifetime(ContainerLifetime.Persistent);
var db = sql.AddDatabase("database");

var core = builder.AddProject<Core>("Core")
                    .WaitFor(db)
                    .WithReference(db);

builder.Build().Run();

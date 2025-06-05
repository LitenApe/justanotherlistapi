using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var core = builder.AddProject<Core>("Core");

builder.Build().Run();

using Microsoft.EntityFrameworkCore;
using JustAnotherListAPI.Checklist;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DatabaseContext>(opt => opt.UseInMemoryDatabase("JustAnotherList"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

ChecklistWebExtension.MapEndpoints(app);

app.Run();

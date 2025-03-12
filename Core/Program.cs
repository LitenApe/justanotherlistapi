using JustAnotherListApi;
using JustAnotherListApi.Checklist;
using Microsoft.EntityFrameworkCore;
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

CreateItem.MapEndpoint(app);
UpdateItem.MapEndpoint(app);
DeleteItem.MapEndpoint(app);

GetItemGroup.MapEndpoint(app);
GetItemGroups.MapEndpoint(app);
CreateItemGroup.MapEndpoint(app);
UpdateItemGroup.MapEndpoint(app);
DeleteItemGroup.MapEndpoint(app);

GetMembers.MapEndpoint(app);
AddMember.MapEndpoint(app);
RemoveMember.MapEndpoint(app);

app.Run();

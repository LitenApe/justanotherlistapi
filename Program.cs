using Microsoft.EntityFrameworkCore;
using JustAnotherListAPI.Checklist;
using JustAnotherListAPI.Common.Generator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DatabaseContext>(opt => opt.UseInMemoryDatabase("JustAnotherList"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
  config.DocumentName = "JustAnotherListAPI";
  config.Title = "JustAnotherList v1";
  config.Version = "v1";

  config.SchemaSettings.SchemaNameGenerator = new PublicSchemaNameGenerator();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseOpenApi();
  app.UseSwaggerUi(config =>
  {
    config.DocumentTitle = "JustAnotherListAPI";
    config.Path = "/swagger";
    config.DocumentPath = "/swagger/{documentName}/swagger.json";
    config.DocExpansion = "list";
  });
}

ChecklistWebExtension.MapEndpoints(app);

app.Run();

// async Task<IResult> GetList(Guid groupId, MemberDb mDb, ItemGroupDb gDp)
// {
//   Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
//   await mDb.Members.Where(m => m.GroupId == groupId && m.MemberId == userId).FirstAsync();
//   var list = await gDp.ItemGroups.Where(l => l.Id == groupId).FirstAsync();
//   return TypedResults.Ok(list);
// }

// async Task<IResult> CreateItem(Guid groupId, ItemDTO item, Repository repository)
// {
//   Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
//   var isMember = await repository.Members.AnyAsync(m => m.GroupId == groupId && m.MemberId == userId);

//   if (!isMember) {
//     return TypedResults.Unauthorized();
//   }

//   Item newItem = new() {
//     Name = item.Name,
//     GroupId = groupId
//   };

// Item item = new () {
//     Id = Guid.CreateVersion7(),
//     Name = "fsf",
//     GroupId = Guid.CreateVersion7()
//   };

//   ItemDTO itemDTO = ItemDTO.Create(item);

//   repository.Items.Add(newItem);

//   await repository.SaveChangesAsync();

  // ItemDTO itemDto = new() {
  //   Id = newItem.Id,
  //   Name = newItem.Name,
  //   Description = newItem.Description
  // };

//   return TypedResults.Created($"/{groupId}/{item.Id}", itemDto);
// }

// async Task<IResult> UpdateItem(Guid groupId, Guid itemId, ItemDTO updatedItem, MemberDb mDb, ItemDb iDb)
// {
//   Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
//   await mDb.Members.Where(m => m.GroupId == groupId && m.MemberId == userId).ToListAsync();

//   var item = await iDb.Items.Where(i => i.Id == itemId).FirstAsync();

//   item.Name = updatedItem.Name;
//   item.Description = updatedItem.Description;
//   item.IsComplete = updatedItem.IsComplete;

//   await iDb.SaveChangesAsync();

//   return TypedResults.NoContent();
// }

// async Task<IResult> DeleteItem(Guid groupId, Guid itemId, MemberDb mDb, ItemDb iDb)
// {
//   Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
//   await mDb.Members.Where(m => m.GroupId == groupId && m.MemberId == userId).ToListAsync();

//   var item = await iDb.Items.Where(i => i.Id == itemId).FirstAsync();

//   if (item.GroupId != groupId)
//   {
//     return TypedResults.BadRequest();
//   }

//   iDb.Remove(item);

//   await iDb.SaveChangesAsync();

//   return TypedResults.NoContent();
// }

// async Task<IResult> GetAllMembers(Guid groupId, MemberDb db)
// {
//   Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
//   var members = await db.Members.Where(m => m.GroupId == groupId).ToListAsync();

//   var isMember = members.Find(m => m.MemberId == userId) is null;

//   if (isMember)
//   {
//     return TypedResults.Unauthorized();
//   }

//   return TypedResults.Ok(members.Select(m => m.MemberId));
// }
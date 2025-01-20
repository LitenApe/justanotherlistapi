using JustAnotherListAPI.Checklist.Model;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListAPI.Checklist.Controller
{
  class ItemController
  {
    public static async Task<IResult> CreateItem (Guid itemGroupId, ItemDTO newItem, DatabaseContext db)
    {
      Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

      var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

      if (!isMember)
      {
        return TypedResults.Forbid();
      }

      var item = Item.Create(itemGroupId, newItem);

      await db.Items.AddAsync(item);

      await db.SaveChangesAsync();

      return TypedResults.Created($"/list/{itemGroupId}/{item.Id}", item);
    }

    public static async Task<IResult> UpdateItem (Guid itemGroupId, Guid itemId, ItemDTO updatedItem, DatabaseContext db)
    {
      Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

      var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

      if (!isMember)
      {
        return TypedResults.Forbid();
      }

      var item = await db.Items.FindAsync(itemId);

      if (item is null)
      {
        return TypedResults.BadRequest();
      }

      item.Update(updatedItem);

      await db.SaveChangesAsync();

      return TypedResults.NoContent();
    }

    public static async Task<IResult> DeleteItem (Guid itemGroupId, Guid itemId, DatabaseContext db)
    {
      Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

      var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

      if (!isMember)
      {
        return TypedResults.Forbid();
      }

      var item = await db.Items.FindAsync(itemId);

      if (item is null)
      {
        return TypedResults.BadRequest();
      }

      db.Items.Remove(item);

      await db.SaveChangesAsync();

      return TypedResults.NoContent();
    }
  }
}
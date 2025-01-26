using Microsoft.EntityFrameworkCore;
using JustAnotherListAPI.Checklist.Model;
using Microsoft.AspNetCore.Mvc;

namespace JustAnotherListAPI.Checklist.Controller
{
    class ItemGroupController
    {
        [ProducesResponseType(StatusCodes.Status200OK)]
        public static async Task<IResult> GetAllItemGroups(DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

            var memberDb = db.Members;
            var itemGroupDb = db.ItemGroups;

            var lists = await memberDb.Where(m => m.MemberId == userId)
              .Join(
                itemGroupDb.Include(ig => ig.Items.Where(i => !i.IsComplete)),
                ig => ig.ItemGroupId,
                m => m.Id,
                (m, ig) => ItemGroupDTO.Create(ig)
              )
              .ToListAsync();

            return TypedResults.Ok(lists);
        }

        public static async Task<IResult> GetItemGroup(Guid itemGroupId, DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

            var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

            if (!isMember)
            {
                return TypedResults.Forbid();
            }

            var items = await db.ItemGroups
              .Include(ig => ig.Items)
              .Include(ig => ig.Members)
              .FirstAsync(ig => ig.Id == itemGroupId);

            return TypedResults.Ok(items);
        }

        [ProducesResponseType(StatusCodes.Status201Created)]
        public static async Task<IResult> CreateItemGroup(ItemGroupDTO newItemGroup, DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

            var itemGroup = ItemGroup.Create(newItemGroup);

            db.ItemGroups.Add(itemGroup);
            db.Members.Add(Member.Create(userId, itemGroup.Id));

            await db.SaveChangesAsync();

            return TypedResults.Created($"/list/{itemGroup.Id}", itemGroup);
        }

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public static async Task<IResult> UpdateItemGroup(Guid itemGroupId, ItemGroupDTO updatedItemGroup, DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
            var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

            if (!isMember)
            {
                return TypedResults.Forbid();
            }

            var itemGroup = await db.ItemGroups.FindAsync(itemGroupId);

            if (itemGroup is null)
            {
                return TypedResults.BadRequest();
            }

            itemGroup.Update(updatedItemGroup);

            await db.SaveChangesAsync();

            return TypedResults.NoContent();
        }

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public static async Task<IResult> DeleteItemGroup(Guid itemGroupId, DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");
            var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

            if (!isMember)
            {
                return TypedResults.Forbid();
            }

            var itemGroup = await db.ItemGroups.FindAsync(itemGroupId);

            if (itemGroup is null)
            {
                return TypedResults.BadRequest();
            }

            db.ItemGroups.Remove(itemGroup);

            await db.SaveChangesAsync();

            return TypedResults.NoContent();
        }
    }
}
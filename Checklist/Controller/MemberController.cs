using JustAnotherListAPI.Checklist.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListAPI.Checklist.Controller
{
    class MemberController
    {
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public static async Task<IResult> GetAllMembers(Guid itemGroupId, DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

            var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

            if (!isMember)
            {
                return TypedResults.Forbid();
            }

            var members = await db.Members
              .Where(m => m.ItemGroupId == itemGroupId)
              .Select(m => m.MemberId)
              .ToListAsync();

            if (members is null)
            {
                return TypedResults.BadRequest();
            }

            return TypedResults.Ok(members);
        }

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public static async Task<IResult> AddMember(Guid itemGroupId, Guid memberId, DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

            var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

            if (!isMember)
            {
                return TypedResults.Forbid();
            }

            db.Members.Add(Member.Create(memberId, itemGroupId));

            await db.SaveChangesAsync();

            return TypedResults.NoContent();
        }

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public static async Task<IResult> RemoveMember(Guid itemGroupId, Guid memberId, DatabaseContext db)
        {
            Guid userId = Guid.Parse("ed1e87c8-4823-4364-b3ee-4d9f13a07300");

            var isMember = await db.Members.AnyAsync(ig => ig.ItemGroupId == itemGroupId && ig.MemberId == userId);

            if (!isMember)
            {
                return TypedResults.Forbid();
            }

            db.Members.Remove(Member.Create(memberId, itemGroupId));

            await db.SaveChangesAsync();

            return TypedResults.NoContent();
        }
    }
}
using Microsoft.EntityFrameworkCore;

namespace JustAnotherListApi
{
    public static class DatabaseContextExtensionIsMember
    {
        public static Task<bool> IsMember(this DatabaseContext db, Guid itemGroup, string user)
        {
            return db.Members.AnyAsync(m => m.ItemGroupId == itemGroup && m.MemberId == user);
        }
    }
}

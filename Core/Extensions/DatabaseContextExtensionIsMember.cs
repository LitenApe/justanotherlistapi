using Microsoft.EntityFrameworkCore;

namespace Core
{
    public static class DatabaseContextExtensionIsMember
    {
        public static Task<bool> IsMember(this DatabaseContext db, Guid itemGroup, string user, CancellationToken ct = default)
        {
            return db.Members.AnyAsync(m => m.ItemGroupId == itemGroup && m.MemberId == user, ct);
        }
    }
}

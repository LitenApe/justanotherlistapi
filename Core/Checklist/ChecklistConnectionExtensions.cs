using System.Data;
using Dapper;

namespace Core.Checklist;

internal static class ChecklistConnectionExtensions
{
    internal static async Task<bool> IsMember(this IDbConnection db, Guid itemGroupId, Guid? userId, CancellationToken ct = default)
    {
        if (userId is null) return false;

        var count = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Members WHERE ItemGroupId = @ItemGroupId AND MemberId = @MemberId",
            new { ItemGroupId = itemGroupId, MemberId = userId },
            cancellationToken: ct));

        return count > 0;
    }
}

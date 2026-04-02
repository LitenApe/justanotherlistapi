using System.Data;
using Dapper;

namespace Core.Checklist;

internal static class ChecklistConnectionExtensions
{
    internal static async Task<bool> IsMember(
        this IDbConnection db,
        Guid itemGroupId,
        Guid? userId,
        CancellationToken ct = default
    )
    {
        if (userId is null)
        {
            return false;
        }

        int count = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Members WHERE ItemGroupId = @ItemGroupId AND MemberId = @MemberId",
                new { ItemGroupId = itemGroupId, MemberId = userId },
                cancellationToken: ct
            )
        );

        return count > 0;
    }

    internal static async Task<bool> IsLastMember(
        this IDbConnection db,
        Guid itemGroupId,
        Guid memberId,
        CancellationToken ct = default
    )
    {
        int result = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT CASE
                    WHEN COUNT(*) = 1
                     AND SUM(CASE WHEN MemberId = @MemberId THEN 1 ELSE 0 END) = 1
                    THEN 1 ELSE 0 END
                FROM Members WHERE ItemGroupId = @ItemGroupId
                """,
                new { ItemGroupId = itemGroupId, MemberId = memberId },
                cancellationToken: ct
            )
        );

        return result == 1;
    }
}

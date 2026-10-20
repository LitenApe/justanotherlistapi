using System.Data;
using Core.AuditLog;
using Dapper;

namespace Core.Checklist;

/// <summary>
/// Deletes item groups whose auto-delete delay has passed since every item was completed.
/// There is no endpoint; <see cref="ChecklistCleanupService"/> runs this on a timer.
/// </summary>
internal static class AutoDeleteItemGroups
{
    internal const string Operation = "AutoDeleteItemGroup";

    internal static async Task<IReadOnlyList<Guid>> Execute(
        IDbConnection db,
        IChecklistNotifier notifier,
        IAuditWriter auditWriter,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> deleted = await DeleteData(now, db, ct);

        foreach (Guid itemGroupId in deleted)
        {
            // No request triggers this, so the audit endpoint filter never sees it.
            auditWriter.Enqueue(
                new AuditEntry(
                    Timestamp: now,
                    TraceId: null,
                    UserId: null,
                    IpAddress: null,
                    ResourceType: "ItemGroup",
                    Operation: Operation,
                    ResourceId: itemGroupId,
                    SubResourceId: null,
                    TargetUserId: null,
                    Outcome: "Success",
                    FailureReason: null
                )
            );
            await notifier.NotifyGroupDeleted(itemGroupId, excludeConnectionId: null);
        }

        return deleted;
    }

    /// <summary>
    /// Deletes every group with auto-delete on, at least one item, no open items, and whose
    /// countdown has run out. The countdown starts at the later of the last item's completion
    /// and the last change to the setting, so turning it on never deletes a list straight away.
    /// Returns the deleted IDs; each is returned once even with several app instances.
    /// </summary>
    internal static async Task<IReadOnlyList<Guid>> DeleteData(
        DateTimeOffset now,
        IDbConnection db,
        CancellationToken ct
    )
    {
        IEnumerable<Guid> deleted = await db.QueryAsync<Guid>(
            new CommandDefinition(
                """
                DELETE ig
                OUTPUT DELETED.Id
                FROM ItemGroups ig
                CROSS APPLY (
                    SELECT
                        COUNT(*) AS ItemCount,
                        COUNT(CASE WHEN i.IsComplete = 0 THEN 1 END) AS OpenCount,
                        MAX(i.CompletedAt) AS LastCompletedAt
                    FROM Items i
                    WHERE i.ItemGroupId = ig.Id
                ) stats
                WHERE ig.AutoDeleteAfterMinutes IS NOT NULL
                    AND stats.ItemCount > 0
                    AND stats.OpenCount = 0
                    AND GREATEST(stats.LastCompletedAt, ig.AutoDeleteUpdatedAt)
                        <= DATEADD(MINUTE, -ig.AutoDeleteAfterMinutes, @Now);
                """,
                new { Now = now },
                cancellationToken: ct
            )
        );

        return deleted.ToList();
    }
}

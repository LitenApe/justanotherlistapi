using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Deletes the lists whose auto-delete countdown has run out. No request asks for this; the
/// cleanup service runs it once a minute.
/// </summary>
public sealed class AutoDeleteItemGroups(
    IItemGroupStore itemGroups,
    IAuditTrail auditTrail,
    IChecklistNotifier notifier,
    TimeProvider timeProvider
)
{
    /// <summary>Deletes the lists that are due, then records and announces each one.</summary>
    /// <returns>The deleted lists' IDs.</returns>
    public async Task<IReadOnlyList<Guid>> Execute(CancellationToken ct)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<Guid> deleted = await itemGroups.DeleteDueForAutoDelete(now, ct);

        foreach (Guid itemGroupId in deleted)
        {
            auditTrail.ItemGroupAutoDeleted(itemGroupId, now);

            // Nobody made this change, so every member hears about it.
            await notifier.NotifyGroupDeleted(itemGroupId, excludeConnectionId: null);
        }

        return deleted;
    }
}

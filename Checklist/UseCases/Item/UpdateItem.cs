using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Replaces an item's name, description and completion.</summary>
public sealed class UpdateItem(
    IMembership membership,
    IItemStore items,
    IChecklistNotifier notifier,
    TimeProvider timeProvider
)
{
    public enum Outcome
    {
        Updated,
        NotAMember,
    }

    /// <summary>
    /// Updates the item and tells the list's other members. An item someone deleted a moment ago
    /// stays deleted: the update still counts, but there's nothing to tell anyone.
    /// </summary>
    public async Task<Outcome> Execute(
        Caller caller,
        Guid itemGroupId,
        Guid itemId,
        ItemInput input,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return Outcome.NotAMember;
        }

        // The stored item carries the completion time, which editing a completed item keeps.
        Item? item = await items.Update(itemGroupId, itemId, input, timeProvider.GetUtcNow(), ct);
        if (item is not null)
        {
            await notifier.NotifyItemUpdated(itemGroupId, item, caller.ConnectionId);
        }

        return Outcome.Updated;
    }
}

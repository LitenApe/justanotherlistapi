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

        DateTimeOffset? completedAt = await items.Update(
            itemGroupId,
            itemId,
            input,
            timeProvider.GetUtcNow(),
            ct
        );

        // The other members get the stored completion time, which editing a completed item keeps.
        var item = new Item
        {
            Id = itemId,
            ItemGroupId = itemGroupId,
            Name = input.Name.Value,
            Description = input.Description,
            IsComplete = input.IsComplete,
            CompletedAt = completedAt,
        };
        await notifier.NotifyItemUpdated(itemGroupId, item, caller.ConnectionId);
        return Outcome.Updated;
    }
}

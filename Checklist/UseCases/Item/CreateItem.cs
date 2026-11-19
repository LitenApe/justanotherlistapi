using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Adds an item to a list. An item added as complete counts as completed now.</summary>
public sealed class CreateItem(
    IMembership membership,
    IItemStore items,
    IChecklistNotifier notifier,
    TimeProvider timeProvider
)
{
    /// <summary>Adds the item and tells the list's other members.</summary>
    /// <returns>The new item, or <see langword="null"/> when the caller isn't a member.</returns>
    public async Task<Item?> Execute(
        Caller caller,
        Guid itemGroupId,
        ItemInput input,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return null;
        }

        var item = new Item
        {
            Id = Guid.NewGuid(),
            ItemGroupId = itemGroupId,
            Name = input.Name.Value,
            Description = input.Description,
            IsComplete = input.IsComplete,
            CompletedAt = input.IsComplete ? timeProvider.GetUtcNow() : null,
        };
        await items.Create(item, ct);
        await notifier.NotifyItemCreated(itemGroupId, item, caller.ConnectionId);
        return item;
    }
}

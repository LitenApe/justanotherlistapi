using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Deletes an item from a list.</summary>
public sealed class DeleteItem(
    IMembership membership,
    IItemStore items,
    IChecklistNotifier notifier
)
{
    public enum Outcome
    {
        Deleted,
        NotAMember,
    }

    public async Task<Outcome> Execute(
        Caller caller,
        Guid itemGroupId,
        Guid itemId,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return Outcome.NotAMember;
        }

        await items.Delete(itemGroupId, itemId, ct);
        await notifier.NotifyItemDeleted(itemGroupId, itemId, caller.ConnectionId);
        return Outcome.Deleted;
    }
}

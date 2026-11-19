using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Deletes a list with its items and members.</summary>
public sealed class DeleteItemGroup(
    IMembership membership,
    IItemGroupStore itemGroups,
    IChecklistNotifier notifier
)
{
    public enum Outcome
    {
        Deleted,
        NotAMember,
    }

    public async Task<Outcome> Execute(Caller caller, Guid itemGroupId, CancellationToken ct)
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return Outcome.NotAMember;
        }

        await itemGroups.Delete(itemGroupId, ct);
        await notifier.NotifyGroupDeleted(itemGroupId, caller.ConnectionId);
        return Outcome.Deleted;
    }
}

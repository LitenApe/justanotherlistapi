using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Renames a list.</summary>
public sealed class UpdateItemGroup(
    IMembership membership,
    IItemGroupStore itemGroups,
    IChecklistNotifier notifier
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
        Name name,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return Outcome.NotAMember;
        }

        await itemGroups.Rename(itemGroupId, name, ct);
        await notifier.NotifyGroupRenamed(itemGroupId, name.Value, caller.ConnectionId);
        return Outcome.Updated;
    }
}

using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Lets a member give another user access to the list.</summary>
public sealed class AddMember(IMembership membership, IChecklistNotifier notifier)
{
    public enum Outcome
    {
        Added,
        NotAMember,
        AlreadyAMember,
    }

    public async Task<Outcome> Execute(
        Caller caller,
        Guid itemGroupId,
        Guid memberId,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return Outcome.NotAMember;
        }

        if (!await membership.Add(itemGroupId, memberId, ct))
        {
            return Outcome.AlreadyAMember;
        }

        await notifier.NotifyMemberAdded(itemGroupId, memberId, caller.ConnectionId);
        return Outcome.Added;
    }
}

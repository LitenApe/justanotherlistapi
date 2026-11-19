using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Lets a member take away another member's access, or leave the list themselves. The last
/// member can't be removed: nobody could reach the list again.
/// </summary>
public sealed class RemoveMember(IMembership membership, IChecklistNotifier notifier)
{
    public enum Outcome
    {
        Removed,
        NotAMember,
        LastMember,
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

        if (!await membership.RemoveUnlessLast(itemGroupId, memberId, ct))
        {
            return Outcome.LastMember;
        }

        await notifier.NotifyMemberRemoved(itemGroupId, memberId, caller.ConnectionId);
        return Outcome.Removed;
    }
}

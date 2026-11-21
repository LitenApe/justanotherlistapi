using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Lets a member give another user access to the list.</summary>
public sealed class AddMember(
    IMembership membership,
    IAccounts accounts,
    IChecklistNotifier notifier
)
{
    public enum Outcome
    {
        Added,
        NotAMember,
        AlreadyAMember,
    }

    /// <summary>What happened, and the new member, with their name, when they were added.</summary>
    public sealed record Result(Outcome Outcome, Member? Member);

    public async Task<Result> Execute(
        Caller caller,
        Guid itemGroupId,
        Guid memberId,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return new Result(Outcome.NotAMember, null);
        }

        if (!await membership.Add(itemGroupId, memberId, ct))
        {
            return new Result(Outcome.AlreadyAMember, null);
        }

        var member = new Member { Id = memberId, Name = (await accounts.Find(memberId, ct))?.Name };
        await notifier.NotifyMemberAdded(itemGroupId, member, caller.ConnectionId);
        return new Result(Outcome.Added, member);
    }
}

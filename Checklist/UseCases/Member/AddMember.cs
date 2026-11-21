using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Lets a member give someone else access to the list, by the email they signed up with. The
/// list's members see the new member's name, never their email.
/// </summary>
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

        /// <summary>No account uses the email.</summary>
        NoAccount,
        AlreadyAMember,
    }

    /// <summary>
    /// What happened, and the person the email belongs to, with their name, once they're known:
    /// for <see cref="Outcome.Added"/> and <see cref="Outcome.AlreadyAMember"/>.
    /// </summary>
    public sealed record Result(Outcome Outcome, Member? Member);

    public async Task<Result> Execute(
        Caller caller,
        Guid itemGroupId,
        EmailAddress email,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return new Result(Outcome.NotAMember, null);
        }

        if (await accounts.FindByEmail(email, ct) is not { } user)
        {
            return new Result(Outcome.NoAccount, null);
        }

        var member = new Member { Id = user.Id, Name = user.Name };
        if (!await membership.Add(itemGroupId, member.Id, ct))
        {
            return new Result(Outcome.AlreadyAMember, member);
        }

        await notifier.NotifyMemberAdded(itemGroupId, member, caller.ConnectionId);
        return new Result(Outcome.Added, member);
    }
}

using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Lets someone change what they're called. Other members see the new name the next time they
/// load a list; nothing is sent to them.
/// </summary>
public sealed class ChangeName(IAccounts accounts)
{
    /// <summary>Renames the caller.</summary>
    /// <returns>
    /// The user with their new name, or <see langword="null"/> when the caller has no account.
    /// </returns>
    public Task<User?> Execute(Caller caller, DisplayName name, CancellationToken ct) =>
        accounts.Rename(caller.UserId, name, ct);
}

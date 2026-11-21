using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Sets a new password with the code from a reset email. Needs no token.</summary>
public sealed class ResetPassword(IAccounts accounts)
{
    public enum Outcome
    {
        Reset,

        /// <summary>The code isn't the account's, has expired or was already used.</summary>
        InvalidCode,
    }

    public async Task<Outcome> Execute(
        Guid userId,
        string code,
        Password password,
        CancellationToken ct
    )
    {
        return await accounts.ResetPassword(userId, code, password, ct)
            ? Outcome.Reset
            : Outcome.InvalidCode;
    }
}

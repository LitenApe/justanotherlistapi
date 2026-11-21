using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Creates an account and signs its owner in. Anyone may, without a token.</summary>
public sealed class SignUp(IAccounts accounts, ITokenIssuer tokens)
{
    /// <summary>Creates the account, unless the email is taken, and issues its first token.</summary>
    /// <returns>
    /// The new session, or <see langword="null"/> when another account already uses the email.
    /// </returns>
    public async Task<Session?> Execute(
        DisplayName name,
        EmailAddress email,
        Password password,
        CancellationToken ct
    )
    {
        return await accounts.Create(name, email, password, ct) is { } user
            ? new Session(tokens.Issue(user), user)
            : null;
    }
}

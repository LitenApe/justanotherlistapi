using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Signs someone in with their email and password. Anyone may try, without a token.</summary>
public sealed class SignIn(IAccounts accounts, ITokenIssuer tokens)
{
    public enum Outcome
    {
        SignedIn,

        /// <summary>No account uses the email, or the password is wrong. Which one isn't said.</summary>
        Wrong,

        /// <summary>Too many wrong passwords in a row; the account takes none for a while.</summary>
        LockedOut,
    }

    /// <summary>What happened, and the new session when it worked.</summary>
    public sealed record Result(Outcome Outcome, Session? Session);

    public async Task<Result> Execute(EmailAddress email, string password, CancellationToken ct)
    {
        PasswordCheck check = await accounts.CheckPassword(email, password, ct);
        return check switch
        {
            { Result: PasswordCheckResult.Matches, User: { } user } => new Result(
                Outcome.SignedIn,
                new Session(tokens.Issue(user), user)
            ),
            { Result: PasswordCheckResult.LockedOut } => new Result(Outcome.LockedOut, null),
            _ => new Result(Outcome.Wrong, null),
        };
    }
}

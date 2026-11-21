using Checklist.Domain;

namespace Checklist.Ports;

/// <summary>
/// The people who can sign in, and their passwords. Passwords never come back out: the adapter
/// keeps them hashed and only says whether one matches.
/// </summary>
public interface IAccounts
{
    /// <summary>
    /// Creates an account and returns its user, or returns <see langword="null"/> when another
    /// account already uses the email, ignoring case. Two concurrent sign-ups with one email must
    /// not both succeed.
    /// </summary>
    Task<User?> Create(
        DisplayName name,
        EmailAddress email,
        Password password,
        CancellationToken ct
    );

    /// <summary>
    /// Checks the password of the account that uses <paramref name="email"/>. A wrong password
    /// counts towards a lockout, which refuses every password for a while; a right one starts
    /// the count again.
    /// </summary>
    Task<PasswordCheck> CheckPassword(EmailAddress email, string password, CancellationToken ct);

    /// <summary>
    /// The user whose account uses <paramref name="email"/>, ignoring case, or
    /// <see langword="null"/> when none does.
    /// </summary>
    Task<User?> FindByEmail(EmailAddress email, CancellationToken ct);

    /// <summary>
    /// A code that lets the owner of <paramref name="email"/> choose a new password, for an hour
    /// and once, or <see langword="null"/> when no account uses the email.
    /// </summary>
    Task<PasswordReset?> CreateResetCode(EmailAddress email, CancellationToken ct);

    /// <summary>
    /// Sets a new password with a code from <see cref="CreateResetCode"/>, and ends any lockout.
    /// Returns <see langword="false"/>, changing nothing, when the code isn't this account's, has
    /// expired or was already used.
    /// </summary>
    Task<bool> ResetPassword(Guid userId, string code, Password password, CancellationToken ct);
}

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
}

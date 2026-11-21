namespace Checklist.Domain;

/// <summary>The answers to checking a password.</summary>
public enum PasswordCheckResult
{
    /// <summary>The password is the account's.</summary>
    Matches,

    /// <summary>No account uses the email, or the password isn't its password.</summary>
    Wrong,

    /// <summary>
    /// Too many wrong passwords in a row: the account takes none, right or wrong, until the
    /// lockout ends.
    /// </summary>
    LockedOut,
}

namespace Checklist.Domain;

/// <summary>What checking someone's password found.</summary>
/// <param name="Result">Whether the password matched.</param>
/// <param name="User">The account's user when the password matched; otherwise <see langword="null"/>.</param>
public sealed record PasswordCheck(PasswordCheckResult Result, User? User);

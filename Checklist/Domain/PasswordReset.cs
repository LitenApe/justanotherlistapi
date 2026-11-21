namespace Checklist.Domain;

/// <summary>A one-time code that lets someone choose a new password, and whose account it's for.</summary>
/// <param name="User">The account the code is for.</param>
/// <param name="Code">The code itself, a secret that only the account's email should carry: never log it.</param>
public sealed record PasswordReset(User User, string Code)
{
    public override string ToString() => $"PasswordReset for {User.Id}";
}

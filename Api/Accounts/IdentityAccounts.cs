using Checklist.Domain;
using Checklist.Ports;
using Microsoft.AspNetCore.Identity;

namespace Api.Accounts;

/// <summary>
/// <see cref="IAccounts"/> on ASP.NET Core Identity: it hashes and checks passwords, and counts
/// wrong ones towards a lockout. <see cref="SqlAccountStore"/> keeps the accounts.
/// </summary>
internal sealed class IdentityAccounts(
    UserManager<Account> users,
    SignInManager<Account> signIns,
    TimeProvider clock
) : IAccounts
{
    public async Task<User?> Create(
        DisplayName name,
        EmailAddress email,
        Password password,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = name.Value,
            Email = email.Value,
            CreatedAt = clock.GetUtcNow(),
        };

        IdentityResult result = await users.CreateAsync(account, password.Value);
        if (result.Succeeded)
        {
            return ToUser(account);
        }

        // Identity's own check finds most duplicates; the store reports the ones that race it.
        return result.Errors.Any(error =>
            error.Code
                is nameof(IdentityErrorDescriber.DuplicateEmail)
                    or nameof(IdentityErrorDescriber.DuplicateUserName)
        )
            ? null
            : throw new InvalidOperationException(
                "Identity refused an account the domain rules accept: "
                    + string.Join(", ", result.Errors.Select(error => error.Code))
            );
    }

    public async Task<PasswordCheck> CheckPassword(
        EmailAddress email,
        string password,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (await users.FindByEmailAsync(email.Value) is not { } account)
        {
            return new PasswordCheck(PasswordCheckResult.Wrong, null);
        }

        SignInResult result = await signIns.CheckPasswordSignInAsync(
            account,
            password,
            lockoutOnFailure: true
        );
        if (result.Succeeded)
        {
            return new PasswordCheck(PasswordCheckResult.Matches, ToUser(account));
        }

        return new PasswordCheck(
            result.IsLockedOut ? PasswordCheckResult.LockedOut : PasswordCheckResult.Wrong,
            null
        );
    }

    private static User ToUser(Account account) =>
        new()
        {
            Id = account.Id,
            Name = account.Name,
            Email = account.Email,
        };
}

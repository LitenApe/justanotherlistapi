using System.Data;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Api.Accounts;

/// <summary>
/// ASP.NET Core Identity's store on the <c>Users</c> table. Identity changes the
/// <see cref="Account"/> it loaded and then calls <see cref="UpdateAsync"/>, which saves
/// everything but the count of wrong passwords: that count changes in the database at once, so
/// that wrong guesses made in parallel all count towards the lockout.
/// </summary>
internal sealed class SqlAccountStore(IDbConnection db)
    : IUserEmailStore<Account>,
        IUserPasswordStore<Account>,
        IUserSecurityStampStore<Account>,
        IUserLockoutStore<Account>
{
    // SQL Server's errors for a row that breaks a unique index or constraint.
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int DuplicateKeyInUniqueConstraint = 2627;

    private const string SelectAccount = """
        SELECT Id, Name, Email, NormalizedEmail, PasswordHash, SecurityStamp, LockoutEnd,
               AccessFailedCount, CreatedAt
        FROM Users
        """;

    /// <inheritdoc/>
    /// <remarks>
    /// The unique index on <c>NormalizedEmail</c> settles two sign-ups with one email at the same
    /// moment: Identity checks for a duplicate first, but both checks can pass before either
    /// insert.
    /// </remarks>
    public async Task<IdentityResult> CreateAsync(Account user, CancellationToken cancellationToken)
    {
        try
        {
            await db.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO Users (Id, Name, Email, NormalizedEmail, PasswordHash, SecurityStamp,
                                       LockoutEnd, AccessFailedCount, CreatedAt)
                    VALUES (@Id, @Name, @Email, @NormalizedEmail, @PasswordHash, @SecurityStamp,
                            @LockoutEnd, @AccessFailedCount, @CreatedAt);
                    """,
                    user,
                    cancellationToken: cancellationToken
                )
            );
            return IdentityResult.Success;
        }
        catch (SqlException ex) when (IsDuplicateKey(ex))
        {
            return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateEmail(user.Email));
        }
    }

    public async Task<IdentityResult> UpdateAsync(Account user, CancellationToken cancellationToken)
    {
        try
        {
            await db.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE Users
                    SET Name = @Name, Email = @Email, NormalizedEmail = @NormalizedEmail,
                        PasswordHash = @PasswordHash, SecurityStamp = @SecurityStamp,
                        LockoutEnd = @LockoutEnd
                    WHERE Id = @Id;
                    """,
                    user,
                    cancellationToken: cancellationToken
                )
            );
            return IdentityResult.Success;
        }
        catch (SqlException ex) when (IsDuplicateKey(ex))
        {
            return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateEmail(user.Email));
        }
    }

    public async Task<IdentityResult> DeleteAsync(Account user, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Users WHERE Id = @Id",
                new { user.Id },
                cancellationToken: cancellationToken
            )
        );
        return IdentityResult.Success;
    }

    public async Task<Account?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        return Guid.TryParse(userId, out Guid id)
            ? await db.QuerySingleOrDefaultAsync<Account>(
                new CommandDefinition(
                    $"{SelectAccount} WHERE Id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken
                )
            )
            : null;
    }

    // The email is the user name, so both lookups are the same.
    public Task<Account?> FindByNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken
    ) => FindByEmailAsync(normalizedUserName, cancellationToken);

    public async Task<Account?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken
    )
    {
        return await db.QuerySingleOrDefaultAsync<Account>(
            new CommandDefinition(
                $"{SelectAccount} WHERE NormalizedEmail = @NormalizedEmail",
                new { NormalizedEmail = normalizedEmail },
                cancellationToken: cancellationToken
            )
        );
    }

    public Task<string> GetUserIdAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email);

    public Task SetUserNameAsync(
        Account user,
        string? userName,
        CancellationToken cancellationToken
    )
    {
        user.Email = userName ?? "";
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(
        Account user,
        CancellationToken cancellationToken
    ) => Task.FromResult<string?>(user.NormalizedEmail);

    public Task SetNormalizedUserNameAsync(
        Account user,
        string? normalizedName,
        CancellationToken cancellationToken
    )
    {
        user.NormalizedEmail = normalizedName ?? "";
        return Task.CompletedTask;
    }

    public Task SetEmailAsync(Account user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email ?? "";
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email);

    // Emails aren't confirmed (specifications/accounts.md), and nothing requires them to be.
    public Task<bool> GetEmailConfirmedAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task SetEmailConfirmedAsync(
        Account user,
        bool confirmed,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    public Task<string?> GetNormalizedEmailAsync(
        Account user,
        CancellationToken cancellationToken
    ) => Task.FromResult<string?>(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(
        Account user,
        string? normalizedEmail,
        CancellationToken cancellationToken
    )
    {
        user.NormalizedEmail = normalizedEmail ?? "";
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(
        Account user,
        string? passwordHash,
        CancellationToken cancellationToken
    )
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash is not null);

    public Task SetSecurityStampAsync(
        Account user,
        string stamp,
        CancellationToken cancellationToken
    )
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(
        Account user,
        CancellationToken cancellationToken
    ) => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(
        Account user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken
    )
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Adds one in the database rather than saving the count Identity loaded, which a parallel
    /// wrong guess may have changed since.
    /// </remarks>
    public async Task<int> IncrementAccessFailedCountAsync(
        Account user,
        CancellationToken cancellationToken
    )
    {
        user.AccessFailedCount = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                UPDATE Users SET AccessFailedCount = AccessFailedCount + 1
                OUTPUT INSERTED.AccessFailedCount
                WHERE Id = @Id;
                """,
                new { user.Id },
                cancellationToken: cancellationToken
            )
        );
        return user.AccessFailedCount;
    }

    public async Task ResetAccessFailedCountAsync(Account user, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET AccessFailedCount = 0 WHERE Id = @Id",
                new { user.Id },
                cancellationToken: cancellationToken
            )
        );
        user.AccessFailedCount = 0;
    }

    public Task<int> GetAccessFailedCountAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult(user.AccessFailedCount);

    // Every account can be locked out.
    public Task<bool> GetLockoutEnabledAsync(Account user, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task SetLockoutEnabledAsync(
        Account user,
        bool enabled,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    // The connection belongs to the request's scope, which disposes it.
    public void Dispose() { }

    private static bool IsDuplicateKey(SqlException ex) =>
        ex.Number is DuplicateKeyInUniqueIndex or DuplicateKeyInUniqueConstraint;
}

namespace Api.Accounts;

/// <summary>
/// An account as ASP.NET Core Identity sees it: one row of <c>Users</c>. It stays inside this
/// adapter; the use cases see a <see cref="Checklist.Domain.User"/>, without the password hash.
/// The email doubles as Identity's user name.
/// </summary>
internal sealed class Account
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    /// <summary>The email as Identity looks it up: upper-cased, so lookups ignore case.</summary>
    public string NormalizedEmail { get; set; } = "";

    public string? PasswordHash { get; set; }

    /// <summary>Changes whenever the password does, which voids the reset codes issued before.</summary>
    public string SecurityStamp { get; set; } = "";

    /// <summary>Until when the account refuses every password, after too many wrong ones.</summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>Wrong passwords in a row since the last right one or the last lockout.</summary>
    public int AccessFailedCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

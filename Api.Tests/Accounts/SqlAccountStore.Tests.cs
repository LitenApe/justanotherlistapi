using Api.Accounts;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Accounts;

public sealed class SqlAccountStoreTests
{
    [Fact]
    public async Task CreateAsync_StoresTheAccount_WhichTheLookupsFind()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        var store = new SqlAccountStore(db);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account kim = NewAccount("Kim@Example.com");

        Assert.Equal(IdentityResult.Success, await store.CreateAsync(kim, ct));

        Assert.Equivalent(kim, await store.FindByIdAsync(kim.Id.ToString(), ct));
        Assert.Equivalent(kim, await store.FindByEmailAsync("KIM@EXAMPLE.COM", ct));
        Assert.Equivalent(kim, await store.FindByNameAsync("KIM@EXAMPLE.COM", ct));
        Assert.Null(await store.FindByIdAsync("not a guid", ct));
        Assert.Null(await store.FindByEmailAsync("LEE@EXAMPLE.COM", ct));
    }

    // Identity looks for a duplicate before it creates an account, but two sign-ups can both
    // look before either inserts. The unique index catches the second.
    [Fact]
    public async Task CreateAsync_ReportsADuplicateEmail_WhenAnotherAccountHasTheSameNormalizedEmail()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        var store = new SqlAccountStore(db);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await store.CreateAsync(NewAccount("kim@example.com"), ct);

        IdentityResult result = await store.CreateAsync(NewAccount("KIM@example.com"), ct);

        Assert.False(result.Succeeded);
        Assert.Equal(
            nameof(IdentityErrorDescriber.DuplicateEmail),
            Assert.Single(result.Errors).Code
        );
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users"));
    }

    [Fact]
    public async Task UpdateAsync_SavesTheAccount_ButNotItsCountOfWrongPasswords()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        var store = new SqlAccountStore(db);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account kim = NewAccount("kim@example.com");
        await store.CreateAsync(kim, ct);
        await store.IncrementAccessFailedCountAsync(kim, ct);

        kim.Name = "Kim Lee";
        kim.PasswordHash = "new hash";
        kim.SecurityStamp = "NEWSTAMP";
        kim.LockoutEnd = new DateTimeOffset(2026, 11, 21, 12, 5, 0, TimeSpan.Zero);
        kim.AccessFailedCount = 0;
        await store.UpdateAsync(kim, ct);

        Account? stored = await store.FindByIdAsync(kim.Id.ToString(), ct);
        Assert.NotNull(stored);
        Assert.Equal(
            ("Kim Lee", "new hash", "NEWSTAMP", kim.LockoutEnd),
            (stored.Name, stored.PasswordHash, stored.SecurityStamp, stored.LockoutEnd)
        );
        // The count stays what the database counted, not the copy's.
        Assert.Equal(1, stored.AccessFailedCount);
    }

    // Two requests load the account, then each counts a wrong password. Both count, though
    // neither copy knew about the other.
    [Fact]
    public async Task IncrementAccessFailedCountAsync_CountsEveryWrongPassword_FromStaleCopiesToo()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        var store = new SqlAccountStore(db);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account kim = NewAccount("kim@example.com");
        await store.CreateAsync(kim, ct);
        Account first = (await store.FindByIdAsync(kim.Id.ToString(), ct))!;
        Account second = (await store.FindByIdAsync(kim.Id.ToString(), ct))!;

        Assert.Equal(1, await store.IncrementAccessFailedCountAsync(first, ct));
        Assert.Equal(2, await store.IncrementAccessFailedCountAsync(second, ct));
        await store.UpdateAsync(first, ct);

        Assert.Equal(2, (await store.FindByIdAsync(kim.Id.ToString(), ct))?.AccessFailedCount);

        await store.ResetAccessFailedCountAsync(first, ct);
        Assert.Equal(0, first.AccessFailedCount);
        Assert.Equal(0, (await store.FindByIdAsync(kim.Id.ToString(), ct))?.AccessFailedCount);
    }

    private static Account NewAccount(string email) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Kim",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "hash",
            SecurityStamp = "STAMP",
            CreatedAt = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero),
        };
}

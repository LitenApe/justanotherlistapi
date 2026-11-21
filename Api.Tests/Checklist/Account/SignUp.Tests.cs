using Checklist.Domain;
using Checklist.UseCases;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Checklist.AccountTests;

public sealed class SignUpTests
{
    [Fact]
    public async Task Execute_CreatesTheAccount_AndSignsItsOwnerIn()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        SignUp signUp = new(services.Accounts(), new FakeTokenIssuer());

        Session? session = await signUp.Execute(
            TestAccounts.ValidDisplayName("Kim"),
            TestAccounts.ValidEmail("Kim@Example.com"),
            TestAccounts.ValidPassword(TestAccounts.KnownPassword),
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(session);
        Assert.Equal("Kim", session.User.Name);
        Assert.Equal("Kim@Example.com", session.User.Email);
        Assert.NotEqual(Guid.Empty, session.User.Id);
        Assert.Equal(new FakeTokenIssuer().Issue(session.User), session.Token);
        // The password is stored hashed, never as typed.
        string? hash = await db.ExecuteScalarAsync<string>(
            "SELECT PasswordHash FROM Users WHERE Id = @Id",
            new { session.User.Id }
        );
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.DoesNotContain("horse", hash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_ReturnsNull_WhenAnotherAccountUsesTheEmail_IgnoringCase()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await services.Accounts().SignUp("kim@example.com");
        SignUp signUp = new(services.Accounts(), new FakeTokenIssuer());

        Session? session = await signUp.Execute(
            TestAccounts.ValidDisplayName("Another Kim"),
            TestAccounts.ValidEmail("KIM@example.COM"),
            TestAccounts.ValidPassword("another long password"),
            TestContext.Current.CancellationToken
        );

        Assert.Null(session);
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users"));
    }
}

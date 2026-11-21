using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Checklist.AccountTests;

public sealed class SignInTests
{
    private const string Email = "kim@example.com";

    [Fact]
    public async Task Execute_SignsIn_WithTheRightPassword_WhateverTheEmailsCase()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        User kim = await services.Accounts().SignUp(Email);

        SignIn.Result result = await Attempt(
            services,
            "KIM@Example.com",
            TestAccounts.KnownPassword
        );

        Assert.Equal(SignIn.Outcome.SignedIn, result.Outcome);
        Assert.Equal(kim, result.Session?.User);
        Assert.Equal(new FakeTokenIssuer().Issue(kim), result.Session?.Token);
    }

    [Fact]
    public async Task Execute_SaysWrong_ForAWrongPasswordAndForAnEmailWithoutAnAccount()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await services.Accounts().SignUp(Email);

        SignIn.Result wrongPassword = await Attempt(services, Email, "not the password");
        SignIn.Result noAccount = await Attempt(services, "lee@example.com", "any password at all");

        Assert.Equal(new SignIn.Result(SignIn.Outcome.Wrong, null), wrongPassword);
        Assert.Equal(new SignIn.Result(SignIn.Outcome.Wrong, null), noAccount);
    }

    [Fact]
    public async Task Execute_LocksTheAccountOut_AfterFiveWrongPasswordsInARow()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await services.Accounts().SignUp(Email);

        for (int attempt = 1; attempt < 5; attempt++)
        {
            Assert.Equal(
                SignIn.Outcome.Wrong,
                (await Attempt(services, Email, "not the password")).Outcome
            );
        }

        Assert.Equal(
            SignIn.Outcome.LockedOut,
            (await Attempt(services, Email, "not the password")).Outcome
        );
        // Even the right password is refused until the lockout ends.
        Assert.Equal(
            new SignIn.Result(SignIn.Outcome.LockedOut, null),
            await Attempt(services, Email, TestAccounts.KnownPassword)
        );
    }

    [Fact]
    public async Task Execute_StartsCountingAgain_AfterTheRightPassword()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await services.Accounts().SignUp(Email);

        for (int round = 0; round < 2; round++)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                await Attempt(services, Email, "not the password");
            }
            Assert.Equal(
                SignIn.Outcome.SignedIn,
                (await Attempt(services, Email, TestAccounts.KnownPassword)).Outcome
            );
        }
    }

    private static Task<SignIn.Result> Attempt(
        ServiceProvider services,
        string email,
        string password
    ) =>
        new SignIn(services.Accounts(), new FakeTokenIssuer()).Execute(
            TestAccounts.ValidEmail(email),
            password,
            TestContext.Current.CancellationToken
        );
}

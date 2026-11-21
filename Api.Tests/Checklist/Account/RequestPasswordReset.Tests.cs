using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Checklist.AccountTests;

public sealed class RequestPasswordResetTests
{
    [Fact]
    public async Task Execute_EmailsTheAccountACode_WhateverTheEmailsCase()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        User kim = await services.Accounts().SignUp("kim@example.com");
        var mailer = new CapturingMailer();

        await new RequestPasswordReset(services.Accounts(), mailer).Execute(
            TestAccounts.ValidEmail("KIM@example.com"),
            TestContext.Current.CancellationToken
        );

        CapturingMailer.SentPasswordReset sent = Assert.Single(mailer.PasswordResets);
        Assert.Equal(kim, sent.User);
        // A link carries the code as it is.
        Assert.Matches("^[A-Za-z0-9_-]+$", sent.Code);
    }

    [Fact]
    public async Task Execute_SendsNothing_WhenNoAccountUsesTheEmail()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        var mailer = new CapturingMailer();

        await new RequestPasswordReset(services.Accounts(), mailer).Execute(
            TestAccounts.ValidEmail("nobody@example.com"),
            TestContext.Current.CancellationToken
        );

        Assert.Empty(mailer.PasswordResets);
    }
}

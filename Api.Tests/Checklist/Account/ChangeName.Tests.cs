using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Checklist.AccountTests;

public sealed class ChangeNameTests
{
    [Fact]
    public async Task Execute_RenamesTheCaller_WhichTheirListsShowFromThenOn()
    {
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        User kim = await services.Accounts().SignUp("kim@example.com", "Kim");
        await db.InsertItemGroup(itemGroupId, kim.Id);

        User? renamed = await new ChangeName(services.Accounts()).Execute(
            new Caller(kim.Id),
            TestAccounts.ValidDisplayName("Kim Lee"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(kim with { Name = "Kim Lee" }, renamed);
        Member member = Assert.Single(
            await new SqlMembership(db).ListMembers(
                itemGroupId,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal("Kim Lee", member.Name);
        // The password still works: renaming changes nothing else.
        Assert.Equal(
            PasswordCheckResult.Matches,
            (
                await services
                    .Accounts()
                    .CheckPassword(
                        TestAccounts.ValidEmail("kim@example.com"),
                        TestAccounts.KnownPassword,
                        TestContext.Current.CancellationToken
                    )
            ).Result
        );
    }

    [Fact]
    public async Task Execute_ReturnsNull_WhenTheCallerHasNoAccount()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);

        User? renamed = await new ChangeName(services.Accounts()).Execute(
            new Caller(Guid.NewGuid()),
            TestAccounts.ValidDisplayName("Nobody"),
            TestContext.Current.CancellationToken
        );

        Assert.Null(renamed);
    }
}

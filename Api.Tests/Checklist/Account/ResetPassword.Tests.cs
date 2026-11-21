using Checklist.Domain;
using Checklist.Ports;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Checklist.AccountTests;

public sealed class ResetPasswordTests
{
    private const string Email = "kim@example.com";
    private const string NewPassword = "a brand new passphrase";

    [Fact]
    public async Task Execute_SetsTheNewPassword_WhichSignsInWhereTheOldOneNoLongerDoes()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        User kim = await services.Accounts().SignUp(Email);
        string code = await RequestCode(services.Accounts());

        ResetPassword.Outcome outcome = await Reset(services.Accounts(), kim.Id, code);

        Assert.Equal(ResetPassword.Outcome.Reset, outcome);
        Assert.Equal(PasswordCheckResult.Matches, (await Check(services, NewPassword)).Result);
        Assert.Equal(
            PasswordCheckResult.Wrong,
            (await Check(services, TestAccounts.KnownPassword)).Result
        );
    }

    [Fact]
    public async Task Execute_WorksOnce_AndVoidsTheOtherCodes()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        User kim = await services.Accounts().SignUp(Email);
        string first = await RequestCode(services.Accounts());
        string second = await RequestCode(services.Accounts());

        await Reset(services.Accounts(), kim.Id, second);

        Assert.Equal(
            ResetPassword.Outcome.InvalidCode,
            await Reset(services.Accounts(), kim.Id, second)
        );
        Assert.Equal(
            ResetPassword.Outcome.InvalidCode,
            await Reset(services.Accounts(), kim.Id, first)
        );
    }

    [Theory]
    [InlineData("not-a-code")]
    [InlineData("%%%")]
    public async Task Execute_RefusesACodeThatIsntOne(string code)
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        User kim = await services.Accounts().SignUp(Email);

        Assert.Equal(
            ResetPassword.Outcome.InvalidCode,
            await Reset(services.Accounts(), kim.Id, code)
        );
        Assert.Equal(
            PasswordCheckResult.Matches,
            (await Check(services, TestAccounts.KnownPassword)).Result
        );
    }

    [Fact]
    public async Task Execute_RefusesAnotherAccountsCode()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await services.Accounts().SignUp(Email);
        User lee = await services.Accounts().SignUp("lee@example.com", "Lee");
        string kimsCode = await RequestCode(services.Accounts());

        Assert.Equal(
            ResetPassword.Outcome.InvalidCode,
            await Reset(services.Accounts(), lee.Id, kimsCode)
        );
        Assert.Equal(
            ResetPassword.Outcome.InvalidCode,
            await Reset(services.Accounts(), Guid.NewGuid(), kimsCode)
        );
    }

    [Fact]
    public async Task Execute_EndsALockout()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        User kim = await services.Accounts().SignUp(Email);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await Check(services, "not the password");
        }
        Assert.Equal(PasswordCheckResult.LockedOut, (await Check(services, "anything")).Result);
        string code = await RequestCode(services.Accounts());

        await Reset(services.Accounts(), kim.Id, code);

        Assert.Equal(PasswordCheckResult.Matches, (await Check(services, NewPassword)).Result);
    }

    private static async Task<string> RequestCode(IAccounts accounts)
    {
        PasswordReset? reset = await accounts.CreateResetCode(
            TestAccounts.ValidEmail(Email),
            TestContext.Current.CancellationToken
        );
        return reset?.Code ?? throw new InvalidOperationException("No account for the email.");
    }

    private static Task<ResetPassword.Outcome> Reset(
        IAccounts accounts,
        Guid userId,
        string code
    ) =>
        new ResetPassword(accounts).Execute(
            userId,
            code,
            TestAccounts.ValidPassword(NewPassword),
            TestContext.Current.CancellationToken
        );

    private static Task<PasswordCheck> Check(ServiceProvider services, string password) =>
        services
            .Accounts()
            .CheckPassword(
                TestAccounts.ValidEmail(Email),
                password,
                TestContext.Current.CancellationToken
            );
}

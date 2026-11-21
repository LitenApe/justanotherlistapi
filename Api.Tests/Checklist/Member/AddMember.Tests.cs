using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Checklist.MemberTests;

public sealed class AddMemberTests
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid kimId = Guid.NewGuid();
    private readonly Guid itemGroupId = Guid.NewGuid();
    private readonly CapturingNotifier notifier = new();

    [Fact]
    public async Task Execute_AddsTheAccountsOwner_ByEmailIgnoringCase_AndTellsTheOtherMembers()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, userId);
        await db.InsertUser(kimId, "Kim", "kim@example.com");

        AddMember.Result result = await Add(db, services, "KIM@Example.com", "conn-am-003");

        var kim = new Member { Id = kimId, Name = "Kim" };
        Assert.Equal(new AddMember.Result(AddMember.Outcome.Added, kim), result);
        Assert.Contains(kimId, await db.LoadMembers(itemGroupId));
        CapturingNotifier.MemberAddedNotification memberAdded =
            Assert.IsType<CapturingNotifier.MemberAddedNotification>(
                Assert.Single(notifier.Notifications)
            );
        Assert.Equal(itemGroupId, memberAdded.GroupId);
        Assert.Equal(kim, memberAdded.Member);
        // The caller's own connection is left out, so it doesn't get its change back.
        Assert.Equal("conn-am-003", memberAdded.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNoAccount_AndChangesNothing_WhenNoAccountUsesTheEmail()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, userId);

        AddMember.Result result = await Add(db, services, "nobody@example.com");

        Assert.Equal(new AddMember.Result(AddMember.Outcome.NoAccount, null), result);
        Assert.Equal(userId, Assert.Single(await db.LoadMembers(itemGroupId)));
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.InsertUser(kimId, "Kim", "kim@example.com");

        AddMember.Result result = await Add(db, services, "kim@example.com");

        Assert.Equal(new AddMember.Result(AddMember.Outcome.NotAMember, null), result);
        Assert.Equal(memberId, Assert.Single(await db.LoadMembers(itemGroupId)));
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task Execute_ReturnsAlreadyAMember_WithWhoTheyAre_WhenTheyAlreadyAreOne()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, userId, kimId);
        await db.InsertUser(kimId, "Kim", "kim@example.com");

        AddMember.Result result = await Add(db, services, "kim@example.com");

        Assert.Equal(
            new AddMember.Result(
                AddMember.Outcome.AlreadyAMember,
                new Member { Id = kimId, Name = "Kim" }
            ),
            result
        );
        Assert.Equal(2, (await db.LoadMembers(itemGroupId)).Count);
        Assert.Empty(notifier.Notifications);
    }

    /// <summary>Adds the owner of <paramref name="email"/> as the user, who may or may not be a member.</summary>
    private Task<AddMember.Result> Add(
        SqlConnection db,
        ServiceProvider services,
        string email,
        string? connectionId = null
    ) =>
        new AddMember(new SqlMembership(db), services.Accounts(), notifier).Execute(
            new Caller(userId, connectionId),
            itemGroupId,
            TestAccounts.ValidEmail(email),
            TestContext.Current.CancellationToken
        );
}

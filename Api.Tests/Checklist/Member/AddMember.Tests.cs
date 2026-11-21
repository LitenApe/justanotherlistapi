using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Checklist.MemberTests;

public sealed class AddMemberTests
{
    [Fact]
    public async Task Execute_AddsTheMember_WithTheirName_AndTellsTheOtherMembers()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, userId);
        await db.InsertUser(newMemberId, "Kim");

        AddMember.Result result = await AddMemberUseCase(db, services, notifier)
            .Execute(
                new Caller(userId, "conn-am-003"),
                itemGroupId,
                newMemberId,
                TestContext.Current.CancellationToken
            );

        var kim = new Member { Id = newMemberId, Name = "Kim" };
        Assert.Equal(new AddMember.Result(AddMember.Outcome.Added, kim), result);
        Assert.Contains(newMemberId, await db.LoadMembers(itemGroupId));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.MemberAddedNotification memberAdded =
            Assert.IsType<CapturingNotifier.MemberAddedNotification>(notification);
        Assert.Equal(itemGroupId, memberAdded.GroupId);
        Assert.Equal(kim, memberAdded.Member);
        // The caller's own connection is left out, so it doesn't get its change back.
        Assert.Equal("conn-am-003", memberAdded.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_AddsSomeoneWithoutAnAccount_WithoutAName()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, userId);

        AddMember.Result result = await AddMemberUseCase(db, services, new CapturingNotifier())
            .Execute(
                new Caller(userId),
                itemGroupId,
                newMemberId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(new Member { Id = newMemberId, Name = null }, result.Member);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, memberId);

        AddMember.Result result = await AddMemberUseCase(db, services, notifier)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                newMemberId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(new AddMember.Result(AddMember.Outcome.NotAMember, null), result);
        Assert.Equal(memberId, Assert.Single(await db.LoadMembers(itemGroupId)));
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task Execute_ReturnsAlreadyAMember_WhenTheUserAlreadyIsOne()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await using ServiceProvider services = TestAccounts.CreateServices(db);
        await db.InsertItemGroup(itemGroupId, userId, existingMemberId);

        AddMember.Result result = await AddMemberUseCase(db, services, notifier)
            .Execute(
                new Caller(userId),
                itemGroupId,
                existingMemberId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(new AddMember.Result(AddMember.Outcome.AlreadyAMember, null), result);
        Assert.Equal(2, (await db.LoadMembers(itemGroupId)).Count);
        Assert.Empty(notifier.Notifications);
    }

    private static AddMember AddMemberUseCase(
        SqlConnection db,
        ServiceProvider services,
        CapturingNotifier notifier
    ) => new(new SqlMembership(db), services.Accounts(), notifier);
}

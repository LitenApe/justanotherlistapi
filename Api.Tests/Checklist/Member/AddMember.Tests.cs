using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.MemberTests;

public sealed class AddMemberTests
{
    [Fact]
    public async Task Execute_AddsTheMember_AndTellsTheOtherMembers()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        AddMember.Outcome outcome = await new AddMember(new SqlMembership(db), notifier).Execute(
            new Caller(userId, "conn-am-003"),
            itemGroupId,
            newMemberId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(AddMember.Outcome.Added, outcome);
        Assert.Contains(newMemberId, await db.LoadMembers(itemGroupId));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.MemberAddedNotification memberAdded =
            Assert.IsType<CapturingNotifier.MemberAddedNotification>(notification);
        Assert.Equal(itemGroupId, memberAdded.GroupId);
        Assert.Equal(newMemberId, memberAdded.MemberId);
        // The caller's own connection is left out, so it doesn't get its change back.
        Assert.Equal("conn-am-003", memberAdded.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);

        AddMember.Outcome outcome = await new AddMember(new SqlMembership(db), notifier).Execute(
            new Caller(Guid.NewGuid()),
            itemGroupId,
            newMemberId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(AddMember.Outcome.NotAMember, outcome);
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
        await db.InsertItemGroup(itemGroupId, userId, existingMemberId);

        AddMember.Outcome outcome = await new AddMember(new SqlMembership(db), notifier).Execute(
            new Caller(userId),
            itemGroupId,
            existingMemberId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(AddMember.Outcome.AlreadyAMember, outcome);
        Assert.Equal(2, (await db.LoadMembers(itemGroupId)).Count);
        Assert.Empty(notifier.Notifications);
    }
}

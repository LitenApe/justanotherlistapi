using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.MemberTests;

public sealed class RemoveMemberTests
{
    [Fact]
    public async Task Execute_RemovesTheMember_AndTellsTheOtherMembers()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId, memberIdToRemove);

        RemoveMember.Outcome outcome = await new RemoveMember(
            new SqlMembership(db),
            notifier
        ).Execute(
            new Caller(userId, "conn-rm-004"),
            itemGroupId,
            memberIdToRemove,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(RemoveMember.Outcome.Removed, outcome);
        Assert.Equal(userId, Assert.Single(await db.LoadMembers(itemGroupId)));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.MemberRemovedNotification memberRemoved =
            Assert.IsType<CapturingNotifier.MemberRemovedNotification>(notification);
        Assert.Equal(itemGroupId, memberRemoved.GroupId);
        Assert.Equal(memberIdToRemove, memberRemoved.MemberId);
        Assert.Equal("conn-rm-004", memberRemoved.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId, Guid.NewGuid());

        RemoveMember.Outcome outcome = await new RemoveMember(
            new SqlMembership(db),
            notifier
        ).Execute(
            new Caller(Guid.NewGuid()),
            itemGroupId,
            memberId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(RemoveMember.Outcome.NotAMember, outcome);
        Assert.Equal(2, (await db.LoadMembers(itemGroupId)).Count);
        Assert.Empty(notifier.Notifications);
    }

    // Freed items need no ItemUpdated: MemberRemoved tells clients to free them too.
    [Fact]
    public async Task Execute_FreesTheItemsReservedForTheRemovedMember()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var memberIdToRemove = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId, memberIdToRemove);
        await db.InsertItem(itemGroupId, itemId, reservedFor: memberIdToRemove, reservedBy: userId);

        RemoveMember.Outcome outcome = await new RemoveMember(
            new SqlMembership(db),
            notifier
        ).Execute(
            new Caller(userId),
            itemGroupId,
            memberIdToRemove,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(RemoveMember.Outcome.Removed, outcome);
        Assert.Null((await db.LoadItem(itemId))?.ReservedFor);
        Assert.IsType<CapturingNotifier.MemberRemovedNotification>(
            Assert.Single(notifier.Notifications)
        );
    }

    [Fact]
    public async Task Execute_FreesTheItemsReservedForAMemberWhoLeaves()
    {
        var userId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId, otherMemberId);
        await db.InsertItem(itemGroupId, itemId, reservedFor: userId);

        RemoveMember.Outcome outcome = await new RemoveMember(
            new SqlMembership(db),
            new CapturingNotifier()
        ).Execute(new Caller(userId), itemGroupId, userId, TestContext.Current.CancellationToken);

        Assert.Equal(RemoveMember.Outcome.Removed, outcome);
        Assert.Null((await db.LoadItem(itemId))?.ReservedFor);
    }

    [Fact]
    public async Task Execute_ReturnsLastMember_WhenTheOnlyMemberLeaves()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        RemoveMember.Outcome outcome = await new RemoveMember(
            new SqlMembership(db),
            notifier
        ).Execute(new Caller(userId), itemGroupId, userId, TestContext.Current.CancellationToken);

        Assert.Equal(RemoveMember.Outcome.LastMember, outcome);
        Assert.Equal(userId, Assert.Single(await db.LoadMembers(itemGroupId)));
        Assert.Empty(notifier.Notifications);
    }

    // Removing someone who isn't a member succeeds without changing anything, so a retried or
    // repeated removal doesn't fail.
    [Fact]
    public async Task Execute_ReturnsRemoved_WhenTheTargetIsNotAMember()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        RemoveMember.Outcome outcome = await new RemoveMember(
            new SqlMembership(db),
            new CapturingNotifier()
        ).Execute(
            new Caller(userId),
            itemGroupId,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(RemoveMember.Outcome.Removed, outcome);
        Assert.Equal(userId, Assert.Single(await db.LoadMembers(itemGroupId)));
    }
}

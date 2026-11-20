using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemTests;

public sealed class ReserveItemTests
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid otherMemberId = Guid.NewGuid();
    private readonly Guid itemGroupId = Guid.NewGuid();
    private readonly Guid itemId = Guid.NewGuid();
    private readonly CapturingNotifier notifier = new();

    [Fact]
    public async Task Execute_ReservesTheItemForAnotherMember_AndTellsTheOtherMembers()
    {
        await using SqlConnection db = await ArrangeList();
        await db.InsertItem(itemGroupId, itemId, "Tent");

        ReserveItem.Outcome outcome = await Reserve(db, otherMemberId, replacing: null);

        Assert.Equal(ReserveItem.Outcome.Reserved, outcome);
        Item? stored = await db.LoadItem(itemId);
        Assert.Equal(otherMemberId, stored?.ReservedFor);
        Assert.Equal(userId, stored?.ReservedBy);
        CapturingNotifier.ItemUpdatedNotification itemUpdated =
            Assert.IsType<CapturingNotifier.ItemUpdatedNotification>(
                Assert.Single(notifier.Notifications)
            );
        Assert.Equal(itemGroupId, itemUpdated.GroupId);
        Assert.Equal(stored, itemUpdated.Item);
        Assert.Equal("conn-reserve", itemUpdated.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_TakesTheItemOver_WhenTheReservationIsTheOneTheCallerSaw()
    {
        await using SqlConnection db = await ArrangeList();
        await db.InsertItem(itemGroupId, itemId, reservedFor: otherMemberId);

        ReserveItem.Outcome outcome = await Reserve(db, userId, replacing: otherMemberId);

        Assert.Equal(ReserveItem.Outcome.Reserved, outcome);
        Assert.Equal(userId, (await db.LoadItem(itemId))?.ReservedFor);
        Assert.Single(notifier.Notifications);
    }

    [Fact]
    public async Task Execute_ReturnsReserved_AndTellsNoOne_WhenNothingChanges()
    {
        await using SqlConnection db = await ArrangeList();
        await db.InsertItem(itemGroupId, itemId, reservedFor: userId);

        // Already reserved that way, then an item someone else deleted a moment ago.
        Assert.Equal(ReserveItem.Outcome.Reserved, await Reserve(db, userId, replacing: null));
        Assert.Equal(
            ReserveItem.Outcome.Reserved,
            await new ReserveItem(new SqlMembership(db), new SqlItemStore(db), notifier).Execute(
                new Caller(userId),
                itemGroupId,
                Guid.NewGuid(),
                userId,
                replacing: null,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, otherMemberId);
        await db.InsertItem(itemGroupId, itemId);

        ReserveItem.Outcome outcome = await Reserve(db, otherMemberId, replacing: null);

        Assert.Equal(ReserveItem.Outcome.NotAMember, outcome);
        Assert.Null((await db.LoadItem(itemId))?.ReservedFor);
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task Execute_RefusesAndTellsNoOne_WhenTheChangeIsNotAllowed()
    {
        await using SqlConnection db = await ArrangeList();
        await db.SetItemGroupSettings(
            itemGroupId,
            new ItemGroupSettings { ProtectReservations = true }
        );
        await db.InsertItem(itemGroupId, itemId, reservedFor: otherMemberId);

        Assert.Equal(ReserveItem.Outcome.Outdated, await Reserve(db, userId, replacing: null));
        Assert.Equal(
            ReserveItem.Outcome.Protected,
            await Reserve(db, userId, replacing: otherMemberId)
        );
        Assert.Equal(
            ReserveItem.Outcome.ForNonMember,
            await Reserve(db, Guid.NewGuid(), replacing: otherMemberId)
        );
        Assert.Equal(otherMemberId, (await db.LoadItem(itemId))?.ReservedFor);
        Assert.Empty(notifier.Notifications);
    }

    private async Task<SqlConnection> ArrangeList()
    {
        SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId, otherMemberId);
        return db;
    }

    private Task<ReserveItem.Outcome> Reserve(SqlConnection db, Guid memberId, Guid? replacing) =>
        new ReserveItem(new SqlMembership(db), new SqlItemStore(db), notifier).Execute(
            new Caller(userId, "conn-reserve"),
            itemGroupId,
            itemId,
            memberId,
            replacing,
            TestContext.Current.CancellationToken
        );
}

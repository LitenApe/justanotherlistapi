using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemTests;

public sealed class UnreserveItemTests
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid otherMemberId = Guid.NewGuid();
    private readonly Guid itemGroupId = Guid.NewGuid();
    private readonly Guid itemId = Guid.NewGuid();
    private readonly CapturingNotifier notifier = new();

    [Fact]
    public async Task Execute_CancelsTheReservation_AndTellsTheOtherMembers()
    {
        await using SqlConnection db = await ArrangeList();
        await db.InsertItem(itemGroupId, itemId, "Tent", reservedFor: otherMemberId);

        UnreserveItem.Outcome outcome = await Unreserve(db, otherMemberId);

        Assert.Equal(UnreserveItem.Outcome.Unreserved, outcome);
        Item? stored = await db.LoadItem(itemId);
        Assert.Null(stored?.ReservedFor);
        Assert.Null(stored?.ReservedBy);
        CapturingNotifier.ItemUpdatedNotification itemUpdated =
            Assert.IsType<CapturingNotifier.ItemUpdatedNotification>(
                Assert.Single(notifier.Notifications)
            );
        Assert.Equal(itemGroupId, itemUpdated.GroupId);
        Assert.Equal(stored, itemUpdated.Item);
        Assert.Equal("conn-unreserve", itemUpdated.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsUnreserved_AndTellsNoOne_WhenTheItemIsAlreadyFree()
    {
        await using SqlConnection db = await ArrangeList();
        await db.InsertItem(itemGroupId, itemId);

        Assert.Equal(UnreserveItem.Outcome.Unreserved, await Unreserve(db, otherMemberId));
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, otherMemberId);
        await db.InsertItem(itemGroupId, itemId, reservedFor: otherMemberId);

        Assert.Equal(UnreserveItem.Outcome.NotAMember, await Unreserve(db, otherMemberId));
        Assert.Equal(otherMemberId, (await db.LoadItem(itemId))?.ReservedFor);
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

        // The caller saw it reserved for someone else; then, seeing it right, isn't involved.
        Assert.Equal(UnreserveItem.Outcome.Outdated, await Unreserve(db, userId));
        Assert.Equal(UnreserveItem.Outcome.Protected, await Unreserve(db, otherMemberId));
        Assert.Equal(otherMemberId, (await db.LoadItem(itemId))?.ReservedFor);
        Assert.Empty(notifier.Notifications);
    }

    private async Task<SqlConnection> ArrangeList()
    {
        SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId, otherMemberId);
        return db;
    }

    private Task<UnreserveItem.Outcome> Unreserve(SqlConnection db, Guid memberId) =>
        new UnreserveItem(new SqlMembership(db), new SqlItemStore(db), notifier).Execute(
            new Caller(userId, "conn-unreserve"),
            itemGroupId,
            itemId,
            memberId,
            TestContext.Current.CancellationToken
        );
}

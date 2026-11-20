using Api.Sql;
using Checklist.Domain;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Sql;

public sealed class SqlItemStoreTests
{
    [Fact]
    public async Task Create_StoresTheItem()
    {
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId);
        var item = new Item
        {
            Id = Guid.NewGuid(),
            ItemGroupId = itemGroupId,
            Name = "Milk",
            Description = "2% fat",
            IsComplete = true,
            CompletedAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
        };

        await new SqlItemStore(db).Create(item, TestContext.Current.CancellationToken);

        Assert.Equal(item, await db.LoadItem(item.Id));
    }

    [Fact]
    public async Task Update_TracksCompletionTime_AcrossCompleteEditAndReopen()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var completedAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId);
        await db.InsertItem(itemGroupId, itemId, "Milk");
        var store = new SqlItemStore(db);

        // Completing sets the time.
        Item? completed = await store.Update(
            itemGroupId,
            itemId,
            Input("Milk", true),
            completedAt,
            ct
        );
        Assert.Equal(completedAt, completed?.CompletedAt);

        // Saving a completed item again keeps the original time.
        Item? edited = await store.Update(
            itemGroupId,
            itemId,
            Input("Oat milk", true),
            completedAt.AddDays(2),
            ct
        );
        Assert.Equal(completedAt, edited?.CompletedAt);

        // Reopening clears it. What Update returns is the row as stored.
        Item? reopened = await store.Update(
            itemGroupId,
            itemId,
            Input("Oat milk", false),
            completedAt.AddDays(3),
            ct
        );
        Assert.NotNull(reopened);
        Assert.Equal("Oat milk", reopened.Name);
        Assert.Null(reopened.CompletedAt);
        Assert.Equal(reopened, await db.LoadItem(itemId));
    }

    [Fact]
    public async Task Update_KeepsTheReservation_WhenTheItemIsEditedCompletedOrReopened()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var reservedFor = Guid.NewGuid();
        var reservedBy = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, reservedFor, reservedBy);
        await db.InsertItem(
            itemGroupId,
            itemId,
            "Tent",
            reservedFor: reservedFor,
            reservedBy: reservedBy
        );
        var store = new SqlItemStore(db);
        ItemInput[] editCompleteReopen =
        [
            Input("Big tent", false),
            Input("Big tent", true),
            Input("Big tent", false),
        ];

        foreach (ItemInput input in editCompleteReopen)
        {
            Item? updated = await store.Update(
                itemGroupId,
                itemId,
                input,
                DateTimeOffset.UnixEpoch,
                ct
            );

            Assert.Equal(reservedFor, updated?.ReservedFor);
            Assert.Equal(reservedBy, updated?.ReservedBy);
        }
        Assert.Equal(reservedFor, (await db.LoadItem(itemId))?.ReservedFor);
    }

    [Fact]
    public async Task Update_ChangesNothing_WhenTheItemIsInAnotherList()
    {
        var itemGroupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId);
        await db.InsertItemGroup(otherGroupId);
        await db.InsertItem(otherGroupId, itemId, "Milk");

        Item? updated = await new SqlItemStore(db).Update(
            itemGroupId,
            itemId,
            Input("Renamed", true),
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken
        );

        Assert.Null(updated);
        Assert.Equal("Milk", (await db.LoadItem(itemId))?.Name);
    }

    [Fact]
    public async Task Delete_OnlyDeletesFromThatList()
    {
        var itemGroupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId);
        await db.InsertItemGroup(otherGroupId);
        await db.InsertItem(itemGroupId, itemId);
        await db.InsertItem(otherGroupId, otherItemId);
        var store = new SqlItemStore(db);

        await store.Delete(itemGroupId, otherItemId, ct);
        await store.Delete(itemGroupId, itemId, ct);

        Assert.Null(await db.LoadItem(itemId));
        Assert.NotNull(await db.LoadItem(otherItemId));
    }

    [Fact]
    public async Task ChangeReservation_ReservesAndCancels_AndReturnsTheItemAsStored()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, alice, bob);
        await db.InsertItem(itemGroupId, itemId, "Tent");
        var store = new SqlItemStore(db);

        // Alice reserves the tent for Bob.
        ReservationResult reserved = await store.ChangeReservation(
            itemGroupId,
            itemId,
            new ReservationChange(alice, bob, Replacing: null),
            ct
        );
        Assert.Equal(ReservationDecision.Change, reserved.Decision);
        Assert.Equal(await db.LoadItem(itemId), reserved.Item);
        Assert.Equal(bob, reserved.Item?.ReservedFor);
        Assert.Equal(alice, reserved.Item?.ReservedBy);

        // Bob cancels it.
        ReservationResult cancelled = await store.ChangeReservation(
            itemGroupId,
            itemId,
            new ReservationChange(bob, MemberId: null, Replacing: bob),
            ct
        );
        Assert.Equal(ReservationDecision.Change, cancelled.Decision);
        Assert.Equal(await db.LoadItem(itemId), cancelled.Item);
        Assert.Null(cancelled.Item?.ReservedFor);
        Assert.Null(cancelled.Item?.ReservedBy);
    }

    [Fact]
    public async Task ChangeReservation_TakesOverAReservation_WhenTheListDoesNotProtectIt()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, alice, bob);
        await db.InsertItem(itemGroupId, itemId, reservedFor: bob);

        ReservationResult result = await new SqlItemStore(db).ChangeReservation(
            itemGroupId,
            itemId,
            new ReservationChange(alice, alice, Replacing: bob),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ReservationDecision.Change, result.Decision);
        Assert.Equal(alice, (await db.LoadItem(itemId))?.ReservedFor);
    }

    [Fact]
    public async Task ChangeReservation_ChangesNothing_WhenTheChangeIsRefused()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, alice, bob, carol);
        await db.SetItemGroupSettings(
            itemGroupId,
            new ItemGroupSettings { ProtectReservations = true }
        );
        await db.InsertItem(itemGroupId, itemId, reservedFor: bob, reservedBy: alice);
        Item? before = await db.LoadItem(itemId);
        var store = new SqlItemStore(db);
        (ReservationChange Change, ReservationDecision Decision)[] refusals =
        [
            // Carol saw the item free: Alice reserved it for Bob before her.
            (new ReservationChange(carol, carol, Replacing: null), ReservationDecision.Outdated),
            // The list protects Bob's reservation from Carol.
            (new ReservationChange(carol, carol, Replacing: bob), ReservationDecision.Protected),
            // Only members can have items reserved for them.
            (
                new ReservationChange(alice, Guid.NewGuid(), Replacing: bob),
                ReservationDecision.ForNonMember
            ),
        ];

        foreach ((ReservationChange change, ReservationDecision decision) in refusals)
        {
            ReservationResult result = await store.ChangeReservation(
                itemGroupId,
                itemId,
                change,
                ct
            );

            Assert.Equal(decision, result.Decision);
            Assert.Null(result.Item);
            Assert.Equal(before, await db.LoadItem(itemId));
        }
    }

    [Fact]
    public async Task ChangeReservation_ChangesNothing_WhenTheItemIsInAnotherList()
    {
        var itemGroupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.InsertItemGroup(otherGroupId, memberId);
        await db.InsertItem(otherGroupId, itemId);

        ReservationResult result = await new SqlItemStore(db).ChangeReservation(
            itemGroupId,
            itemId,
            new ReservationChange(memberId, memberId, Replacing: null),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new ReservationResult(ReservationDecision.Unchanged, Item: null), result);
        Assert.Null((await db.LoadItem(itemId))?.ReservedFor);
    }

    // The app's scoped connection starts closed, unlike TestDatabase connections, and a
    // transaction needs an open one.
    [Fact]
    public async Task ChangeReservation_OpensTheConnection_WhenItIsClosed()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.InsertItem(itemGroupId, itemId);
        await db.CloseAsync();

        ReservationResult result = await new SqlItemStore(db).ChangeReservation(
            itemGroupId,
            itemId,
            new ReservationChange(memberId, memberId, Replacing: null),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ReservationDecision.Change, result.Decision);
        Assert.Equal(memberId, (await db.LoadItem(itemId))?.ReservedFor);
    }

    private static ItemInput Input(string name, bool isComplete) =>
        new(TestHelpers.ValidName(name), Description: null, isComplete);
}

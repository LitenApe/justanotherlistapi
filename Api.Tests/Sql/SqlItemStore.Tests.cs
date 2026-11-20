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

    private static ItemInput Input(string name, bool isComplete) =>
        new(TestHelpers.ValidName(name), Description: null, isComplete);
}

using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemTests;

public sealed class UpdateItemTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 19, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_UpdatesTheItem_AndSendsTheStoredCompletionTime()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);
        await db.InsertItem(itemGroupId, itemId, "Old Item");

        UpdateItem.Outcome outcome = await UpdateUseCase(db, notifier)
            .Execute(
                new Caller(userId, "conn-xyz-789"),
                itemGroupId,
                itemId,
                new ItemInput(TestHelpers.ValidName("Updated Item"), "Updated", IsComplete: true),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(UpdateItem.Outcome.Updated, outcome);
        Item? stored = await db.LoadItem(itemId);
        Assert.NotNull(stored);
        Assert.Equal("Updated Item", stored.Name);
        Assert.Equal("Updated", stored.Description);
        Assert.True(stored.IsComplete);
        Assert.Equal(now, stored.CompletedAt);
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.ItemUpdatedNotification itemUpdated =
            Assert.IsType<CapturingNotifier.ItemUpdatedNotification>(notification);
        Assert.Equal(itemGroupId, itemUpdated.GroupId);
        Assert.Equal(stored, itemUpdated.Item);
        Assert.Equal("conn-xyz-789", itemUpdated.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());
        await db.InsertItem(itemGroupId, itemId, "Old Item");

        UpdateItem.Outcome outcome = await UpdateUseCase(db, notifier)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                itemId,
                new ItemInput(TestHelpers.ValidName("Name"), null, IsComplete: false),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(UpdateItem.Outcome.NotAMember, outcome);
        Assert.Equal("Old Item", (await db.LoadItem(itemId))?.Name);
        Assert.Empty(notifier.Notifications);
    }

    // Updating an item that doesn't exist succeeds without creating it, so a member editing an
    // item someone else just deleted doesn't get an error.
    [Fact]
    public async Task Execute_ReturnsUpdated_WithoutCreatingAnything_WhenTheItemDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        UpdateItem.Outcome outcome = await UpdateUseCase(db, new CapturingNotifier())
            .Execute(
                new Caller(userId),
                itemGroupId,
                itemId,
                new ItemInput(TestHelpers.ValidName("Name"), null, IsComplete: false),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(UpdateItem.Outcome.Updated, outcome);
        Assert.Null(await db.LoadItem(itemId));
    }

    private static UpdateItem UpdateUseCase(SqlConnection db, CapturingNotifier notifier) =>
        new(new SqlMembership(db), new SqlItemStore(db), notifier, new FixedTimeProvider(now));
}

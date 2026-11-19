using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemTests;

public sealed class CreateItemTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 19, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_AddsTheItem_AndTellsTheOtherMembers()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        Item? item = await CreateUseCase(db, notifier)
            .Execute(
                new Caller(userId, "conn-abc-123"),
                itemGroupId,
                new ItemInput(TestHelpers.ValidName("Milk"), "2% fat", IsComplete: false),
                TestContext.Current.CancellationToken
            );

        Assert.NotNull(item);
        Assert.Equal("Milk", item.Name);
        Assert.Equal("2% fat", item.Description);
        Assert.False(item.IsComplete);
        Assert.Null(item.CompletedAt);
        Assert.Equal(itemGroupId, item.ItemGroupId);
        Assert.Equal(item, await db.LoadItem(item.Id));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.ItemCreatedNotification itemCreated =
            Assert.IsType<CapturingNotifier.ItemCreatedNotification>(notification);
        Assert.Equal(itemGroupId, itemCreated.GroupId);
        Assert.Equal(item, itemCreated.Item);
        Assert.Equal("conn-abc-123", itemCreated.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_CompletesTheItemNow_WhenItIsAddedComplete()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        Item? item = await CreateUseCase(db, new CapturingNotifier())
            .Execute(
                new Caller(userId),
                itemGroupId,
                new ItemInput(TestHelpers.ValidName("Milk"), null, IsComplete: true),
                TestContext.Current.CancellationToken
            );

        Assert.NotNull(item);
        Assert.Equal(now, item.CompletedAt);
        Assert.Equal(now, (await db.LoadItem(item.Id))?.CompletedAt);
    }

    [Fact]
    public async Task Execute_ReturnsNull_AndAddsNothing_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());

        Item? item = await CreateUseCase(db, notifier)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                new ItemInput(TestHelpers.ValidName("Milk"), null, IsComplete: false),
                TestContext.Current.CancellationToken
            );

        Assert.Null(item);
        Assert.Empty(notifier.Notifications);
    }

    private static CreateItem CreateUseCase(SqlConnection db, CapturingNotifier notifier) =>
        new(new SqlMembership(db), new SqlItemStore(db), notifier, new FixedTimeProvider(now));
}

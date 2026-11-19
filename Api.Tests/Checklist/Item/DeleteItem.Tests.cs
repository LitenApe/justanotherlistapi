using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemTests;

public sealed class DeleteItemTests
{
    [Fact]
    public async Task Execute_DeletesTheItem_AndTellsTheOtherMembers()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);
        await db.InsertItem(itemGroupId, itemId);

        DeleteItem.Outcome outcome = await DeleteUseCase(db, notifier)
            .Execute(
                new Caller(userId, "conn-del-001"),
                itemGroupId,
                itemId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(DeleteItem.Outcome.Deleted, outcome);
        Assert.Null(await db.LoadItem(itemId));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.ItemDeletedNotification itemDeleted =
            Assert.IsType<CapturingNotifier.ItemDeletedNotification>(notification);
        Assert.Equal(itemGroupId, itemDeleted.GroupId);
        Assert.Equal(itemId, itemDeleted.ItemId);
        Assert.Equal("conn-del-001", itemDeleted.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndKeepsTheItem_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());
        await db.InsertItem(itemGroupId, itemId);

        DeleteItem.Outcome outcome = await DeleteUseCase(db, notifier)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                itemId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(DeleteItem.Outcome.NotAMember, outcome);
        Assert.NotNull(await db.LoadItem(itemId));
        Assert.Empty(notifier.Notifications);
    }

    // Deleting an item that's already gone succeeds, so a repeated delete doesn't fail.
    [Fact]
    public async Task Execute_ReturnsDeleted_WhenTheItemDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        DeleteItem.Outcome outcome = await DeleteUseCase(db, new CapturingNotifier())
            .Execute(
                new Caller(userId),
                itemGroupId,
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(DeleteItem.Outcome.Deleted, outcome);
    }

    private static DeleteItem DeleteUseCase(SqlConnection db, CapturingNotifier notifier) =>
        new(new SqlMembership(db), new SqlItemStore(db), notifier);
}

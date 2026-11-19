using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class DeleteItemGroupTests
{
    [Fact]
    public async Task Execute_DeletesTheList_AndTellsTheOtherMembers()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        DeleteItemGroup.Outcome outcome = await DeleteUseCase(db, notifier)
            .Execute(
                new Caller(userId, "conn-delete-001"),
                itemGroupId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(DeleteItemGroup.Outcome.Deleted, outcome);
        Assert.Null(await db.LoadItemGroupName(itemGroupId));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupDeletedNotification groupDeleted =
            Assert.IsType<CapturingNotifier.GroupDeletedNotification>(notification);
        Assert.Equal(itemGroupId, groupDeleted.GroupId);
        Assert.Equal("conn-delete-001", groupDeleted.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndKeepsTheList_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());

        DeleteItemGroup.Outcome outcome = await DeleteUseCase(db, notifier)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(DeleteItemGroup.Outcome.NotAMember, outcome);
        Assert.NotNull(await db.LoadItemGroupName(itemGroupId));
        Assert.Empty(notifier.Notifications);
    }

    private static DeleteItemGroup DeleteUseCase(SqlConnection db, CapturingNotifier notifier) =>
        new(new SqlMembership(db), new SqlItemGroupStore(db), notifier);
}

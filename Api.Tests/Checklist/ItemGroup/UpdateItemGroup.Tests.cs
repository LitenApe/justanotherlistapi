using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class UpdateItemGroupTests
{
    [Fact]
    public async Task Execute_RenamesTheList_AndTellsTheOtherMembers()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        UpdateItemGroup.Outcome outcome = await UpdateUseCase(db, notifier)
            .Execute(
                new Caller(userId, "conn-rename-001"),
                itemGroupId,
                TestHelpers.ValidName("Groceries"),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(UpdateItemGroup.Outcome.Updated, outcome);
        Assert.Equal("Groceries", await db.LoadItemGroupName(itemGroupId));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupRenamedNotification renamed =
            Assert.IsType<CapturingNotifier.GroupRenamedNotification>(notification);
        Assert.Equal(itemGroupId, renamed.GroupId);
        Assert.Equal("Groceries", renamed.Name);
        Assert.Equal("conn-rename-001", renamed.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNotAMember_AndKeepsTheName_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());

        UpdateItemGroup.Outcome outcome = await UpdateUseCase(db, notifier)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                TestHelpers.ValidName("Groceries"),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(UpdateItemGroup.Outcome.NotAMember, outcome);
        Assert.Equal("Group", await db.LoadItemGroupName(itemGroupId));
        Assert.Empty(notifier.Notifications);
    }

    private static UpdateItemGroup UpdateUseCase(SqlConnection db, CapturingNotifier notifier) =>
        new(new SqlMembership(db), new SqlItemGroupStore(db), notifier);
}

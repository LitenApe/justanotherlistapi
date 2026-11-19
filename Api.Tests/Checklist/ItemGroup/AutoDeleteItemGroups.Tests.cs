using Api.AuditLog;
using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemGroupTests;

// Which lists are due is the store's rule; SqlItemGroupStore's tests cover it case by case.
public sealed class AutoDeleteItemGroupsTests
{
    private const int OneDay = 24 * 60;
    private static readonly DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_DeletesTheDueLists_ThenAuditsAndAnnouncesEachOne()
    {
        var dueId = Guid.NewGuid();
        var notDueId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        var auditWriter = new CapturingAuditWriter();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await InsertAutoDeleteList(db, dueId, lastCompletedAt: now.AddDays(-2));
        await InsertAutoDeleteList(db, notDueId, lastCompletedAt: now.AddHours(-1));

        IReadOnlyList<Guid> deleted = await AutoDeleteUseCase(db, notifier, auditWriter)
            .Execute(TestContext.Current.CancellationToken);

        Assert.Equal(dueId, Assert.Single(deleted));
        Assert.Null(await db.LoadItemGroupName(dueId));
        Assert.NotNull(await db.LoadItemGroupName(notDueId));

        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupDeletedNotification groupDeleted =
            Assert.IsType<CapturingNotifier.GroupDeletedNotification>(notification);
        Assert.Equal(dueId, groupDeleted.GroupId);
        Assert.Null(groupDeleted.ExcludeConnectionId);

        AuditEntry entry = Assert.Single(auditWriter.Entries);
        Assert.Equal("AutoDeleteItemGroup", entry.Operation);
        Assert.Equal("ItemGroup", entry.ResourceType);
        Assert.Equal(dueId, entry.ResourceId);
        Assert.Equal("Success", entry.Outcome);
        Assert.Equal(now, entry.Timestamp);
        Assert.Null(entry.UserId);
        Assert.Null(entry.IpAddress);
        Assert.Null(entry.TraceId);
    }

    [Fact]
    public async Task Execute_DoesNothing_WhenNoListIsDue()
    {
        var notifier = new CapturingNotifier();
        var auditWriter = new CapturingAuditWriter();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await InsertAutoDeleteList(db, Guid.NewGuid(), lastCompletedAt: now.AddHours(-1));

        IReadOnlyList<Guid> deleted = await AutoDeleteUseCase(db, notifier, auditWriter)
            .Execute(TestContext.Current.CancellationToken);

        Assert.Empty(deleted);
        Assert.Empty(notifier.Notifications);
        Assert.Empty(auditWriter.Entries);
    }

    /// <summary>A list that auto-deletes a day after its one item was completed.</summary>
    private static async Task InsertAutoDeleteList(
        SqlConnection db,
        Guid itemGroupId,
        DateTimeOffset lastCompletedAt
    )
    {
        await db.InsertItemGroup(itemGroupId);
        await db.SetItemGroupSettings(
            itemGroupId,
            new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = OneDay,
                AutoDeleteUpdatedAt = now.AddDays(-10),
            }
        );
        await db.InsertItem(itemGroupId, Guid.NewGuid(), completedAt: lastCompletedAt);
    }

    private static AutoDeleteItemGroups AutoDeleteUseCase(
        SqlConnection db,
        CapturingNotifier notifier,
        CapturingAuditWriter auditWriter
    ) =>
        new(
            new SqlItemGroupStore(db),
            new AuditTrail(auditWriter),
            notifier,
            new FixedTimeProvider(now)
        );
}

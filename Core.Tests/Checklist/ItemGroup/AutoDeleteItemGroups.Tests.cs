using Core.AuditLog;
using Core.Checklist;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class AutoDeleteItemGroupsTests
{
    private const int OneDay = 24 * 60;
    private static readonly DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeleteData_DeletesGroup_WhenEveryItemWasCompletedLongerAgoThanTheDelay()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid groupId = await InsertGroup(db, OneDay, autoDeleteUpdatedAt: now.AddDays(-10));
        await InsertItem(db, groupId, completedAt: now.AddDays(-3));
        await InsertItem(db, groupId, completedAt: now.AddDays(-1));

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.DeleteData(
            now,
            db,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(groupId, Assert.Single(deleted));
        Assert.Equal(0, await CountGroups(db, groupId));
        Assert.Equal(
            0,
            await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Items WHERE ItemGroupId = @Id",
                new { Id = groupId }
            )
        );
    }

    [Fact]
    public async Task DeleteData_KeepsGroup_UntilTheDelayHasPassedSinceTheLastCompletion()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid groupId = await InsertGroup(db, OneDay, autoDeleteUpdatedAt: now.AddDays(-10));
        await InsertItem(db, groupId, completedAt: now.AddDays(-5));
        await InsertItem(db, groupId, completedAt: now.AddDays(-1).AddSeconds(1));

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.DeleteData(
            now,
            db,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(deleted);
        Assert.Equal(1, await CountGroups(db, groupId));
    }

    [Fact]
    public async Task DeleteData_KeepsGroup_WhenAnItemIsOpen()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid groupId = await InsertGroup(db, OneDay, autoDeleteUpdatedAt: now.AddDays(-10));
        await InsertItem(db, groupId, completedAt: now.AddDays(-5));
        await InsertItem(db, groupId, completedAt: null);

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.DeleteData(
            now,
            db,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(deleted);
        Assert.Equal(1, await CountGroups(db, groupId));
    }

    [Fact]
    public async Task DeleteData_KeepsGroup_WhenItHasNoItems()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid groupId = await InsertGroup(db, OneDay, autoDeleteUpdatedAt: now.AddDays(-10));

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.DeleteData(
            now,
            db,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(deleted);
        Assert.Equal(1, await CountGroups(db, groupId));
    }

    [Fact]
    public async Task DeleteData_KeepsGroup_WhenAutoDeleteIsOff()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid groupId = await InsertGroup(db, null, autoDeleteUpdatedAt: null);
        await InsertItem(db, groupId, completedAt: now.AddDays(-400));

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.DeleteData(
            now,
            db,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(deleted);
        Assert.Equal(1, await CountGroups(db, groupId));
    }

    [Fact]
    public async Task DeleteData_StartsCountdownWhenTheSettingChanged_IfItemsWereCompletedEarlier()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid recentlyEnabled = await InsertGroup(db, OneDay, autoDeleteUpdatedAt: now.AddHours(-1));
        await InsertItem(db, recentlyEnabled, completedAt: now.AddDays(-30));
        Guid enabledLongAgo = await InsertGroup(db, OneDay, autoDeleteUpdatedAt: now.AddDays(-2));
        await InsertItem(db, enabledLongAgo, completedAt: now.AddDays(-30));

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.DeleteData(
            now,
            db,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(enabledLongAgo, Assert.Single(deleted));
        Assert.Equal(1, await CountGroups(db, recentlyEnabled));
    }

    [Fact]
    public async Task Execute_AuditsAndNotifiesEachDeletedGroup()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid groupId = await InsertGroup(db, OneDay, autoDeleteUpdatedAt: now.AddDays(-10));
        await InsertItem(db, groupId, completedAt: now.AddDays(-2));
        var notifier = new CapturingNotifier();
        var auditWriter = new CapturingAuditWriter();

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.Execute(
            db,
            notifier,
            auditWriter,
            now,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(groupId, Assert.Single(deleted));

        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupDeletedNotification groupDeleted =
            Assert.IsType<CapturingNotifier.GroupDeletedNotification>(notification);
        Assert.Equal(groupId, groupDeleted.GroupId);
        Assert.Null(groupDeleted.ExcludeConnectionId);

        AuditEntry entry = Assert.Single(auditWriter.Entries);
        Assert.Equal("AutoDeleteItemGroup", entry.Operation);
        Assert.Equal("ItemGroup", entry.ResourceType);
        Assert.Equal(groupId, entry.ResourceId);
        Assert.Equal("Success", entry.Outcome);
        Assert.Null(entry.UserId);
        Assert.Equal(now, entry.Timestamp);
    }

    [Fact]
    public async Task Execute_DoesNothing_WhenNoGroupIsDue()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        var notifier = new CapturingNotifier();
        var auditWriter = new CapturingAuditWriter();

        IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.Execute(
            db,
            notifier,
            auditWriter,
            now,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(deleted);
        Assert.Empty(notifier.Notifications);
        Assert.Empty(auditWriter.Entries);
    }

    private static async Task<Guid> InsertGroup(
        SqlConnection db,
        int? autoDeleteAfterMinutes,
        DateTimeOffset? autoDeleteUpdatedAt
    )
    {
        var id = Guid.NewGuid();
        await db.ExecuteAsync(
            """
            INSERT INTO ItemGroups (Id, Name, AutoDeleteAfterMinutes, AutoDeleteUpdatedAt)
            VALUES (@Id, @Name, @AutoDeleteAfterMinutes, @AutoDeleteUpdatedAt)
            """,
            new
            {
                Id = id,
                Name = "Group",
                AutoDeleteAfterMinutes = autoDeleteAfterMinutes,
                AutoDeleteUpdatedAt = autoDeleteUpdatedAt,
            }
        );
        return id;
    }

    private static async Task InsertItem(
        SqlConnection db,
        Guid groupId,
        DateTimeOffset? completedAt
    ) =>
        await db.ExecuteAsync(
            """
            INSERT INTO Items (Id, Name, IsComplete, CompletedAt, ItemGroupId)
            VALUES (@Id, @Name, @IsComplete, @CompletedAt, @ItemGroupId)
            """,
            new
            {
                Id = Guid.NewGuid(),
                Name = "Item",
                IsComplete = completedAt is not null,
                CompletedAt = completedAt,
                ItemGroupId = groupId,
            }
        );

    private static Task<int> CountGroups(SqlConnection db, Guid groupId) =>
        db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ItemGroups WHERE Id = @Id",
            new { Id = groupId }
        );
}

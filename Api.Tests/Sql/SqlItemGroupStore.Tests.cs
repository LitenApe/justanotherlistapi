using Api.Sql;
using Checklist.Domain;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Sql;

public sealed class SqlItemGroupStoreTests
{
    private const int OneDay = 24 * 60;
    private static readonly DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_StoresTheList_WithItsFirstMember()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();

        await new SqlItemGroupStore(db).Create(
            itemGroupId,
            TestHelpers.ValidName("Groceries"),
            memberId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Groceries", await db.LoadItemGroupName(itemGroupId));
        Assert.Equal(memberId, Assert.Single(await db.LoadMembers(itemGroupId)));
    }

    // The app's scoped connection starts closed, unlike TestDatabase connections, and a
    // transaction needs an open one.
    [Fact]
    public async Task Create_OpensTheConnection_WhenItIsClosed()
    {
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.CloseAsync();

        await new SqlItemGroupStore(db).Create(
            itemGroupId,
            TestHelpers.ValidName("Groceries"),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Groceries", await db.LoadItemGroupName(itemGroupId));
    }

    [Fact]
    public async Task Load_ReturnsTheList_WithAllItemsMembersAndSettings()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var settings = new ItemGroupSettings
        {
            AutoDeleteAfterMinutes = OneDay,
            HideCompletedAfterMinutes = 30,
            AutoDeleteUpdatedAt = now,
        };
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.SetItemGroupSettings(itemGroupId, settings);
        await db.InsertItem(itemGroupId, itemId, "Milk", completedAt: now);

        ItemGroup? itemGroup = await new SqlItemGroupStore(db).Load(
            itemGroupId,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(itemGroup);
        Assert.Equal(itemGroupId, itemGroup.Id);
        Assert.Equal("Group", itemGroup.Name);
        Assert.Equal(await db.LoadItem(itemId), Assert.Single(itemGroup.Items));
        Assert.Equal(memberId, Assert.Single(itemGroup.Members));
        Assert.Equal(settings, itemGroup.Settings);
    }

    [Fact]
    public async Task Load_ReturnsNull_WhenTheListDoesNotExist()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();

        Assert.Null(
            await new SqlItemGroupStore(db).Load(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateSettings_RestartsTheCountdown_OnlyWhenAutoDeleteChanges()
    {
        var itemGroupId = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId);
        var store = new SqlItemGroupStore(db);

        // Turning auto-delete on starts the countdown.
        Assert.Equal(
            new ItemGroupSettings { AutoDeleteAfterMinutes = OneDay, AutoDeleteUpdatedAt = now },
            await store.UpdateSettings(
                itemGroupId,
                TestHelpers.ValidSettings(OneDay, null),
                now,
                ct
            )
        );

        // Changing only the hide setting keeps it.
        Assert.Equal(
            new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = OneDay,
                HideCompletedAfterMinutes = 30,
                AutoDeleteUpdatedAt = now,
            },
            await store.UpdateSettings(
                itemGroupId,
                TestHelpers.ValidSettings(OneDay, 30),
                now.AddHours(1),
                ct
            )
        );

        // Changing the delay restarts it.
        Assert.Equal(
            new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = 2 * OneDay,
                HideCompletedAfterMinutes = 30,
                AutoDeleteUpdatedAt = now.AddHours(2),
            },
            await store.UpdateSettings(
                itemGroupId,
                TestHelpers.ValidSettings(2 * OneDay, 30),
                now.AddHours(2),
                ct
            )
        );

        // Turning it off is a change too.
        var off = new ItemGroupSettings { AutoDeleteUpdatedAt = now.AddHours(3) };
        Assert.Equal(
            off,
            await store.UpdateSettings(
                itemGroupId,
                TestHelpers.ValidSettings(null, null),
                now.AddHours(3),
                ct
            )
        );
        Assert.Equal(off, await db.LoadSettings(itemGroupId));
    }

    [Fact]
    public async Task UpdateSettings_ReturnsNull_WhenTheListDoesNotExist()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();

        Assert.Null(
            await new SqlItemGroupStore(db).UpdateSettings(
                Guid.NewGuid(),
                TestHelpers.ValidSettings(OneDay, null),
                now,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Delete_DeletesTheList_WithItsItemsAndMembers_AndNothingElse()
    {
        var itemGroupId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.InsertItem(itemGroupId, Guid.NewGuid());
        await db.InsertItemGroup(otherId, memberId);
        await db.InsertItem(otherId, Guid.NewGuid());

        await new SqlItemGroupStore(db).Delete(itemGroupId, TestContext.Current.CancellationToken);

        Assert.Null(await db.LoadItemGroupName(itemGroupId));
        Assert.Equal(0, await db.CountItems(itemGroupId));
        Assert.Empty(await db.LoadMembers(itemGroupId));
        Assert.NotNull(await db.LoadItemGroupName(otherId));
        Assert.Equal(1, await db.CountItems(otherId));
        Assert.Equal(memberId, Assert.Single(await db.LoadMembers(otherId)));
    }

    [Fact]
    public async Task DeleteDueForAutoDelete_DeletesTheList_WhenEveryItemWasCompletedLongerAgoThanTheDelay()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid itemGroupId = await InsertList(
            db,
            OneDay,
            settingChangedAt: now.AddDays(-10),
            now.AddDays(-3),
            now.AddDays(-1)
        );

        IReadOnlyList<Guid> deleted = await DeleteDue(db);

        Assert.Equal(itemGroupId, Assert.Single(deleted));
        Assert.Null(await db.LoadItemGroupName(itemGroupId));
        Assert.Equal(0, await db.CountItems(itemGroupId));
    }

    [Fact]
    public async Task DeleteDueForAutoDelete_KeepsTheList_UntilTheDelayHasPassedSinceTheLastCompletion()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid itemGroupId = await InsertList(
            db,
            OneDay,
            settingChangedAt: now.AddDays(-10),
            now.AddDays(-5),
            now.AddDays(-1).AddSeconds(1)
        );

        Assert.Empty(await DeleteDue(db));
        Assert.NotNull(await db.LoadItemGroupName(itemGroupId));
    }

    [Fact]
    public async Task DeleteDueForAutoDelete_KeepsTheList_WhenAnItemIsOpen()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid itemGroupId = await InsertList(
            db,
            OneDay,
            settingChangedAt: now.AddDays(-10),
            now.AddDays(-5),
            null
        );

        Assert.Empty(await DeleteDue(db));
        Assert.NotNull(await db.LoadItemGroupName(itemGroupId));
    }

    [Fact]
    public async Task DeleteDueForAutoDelete_KeepsTheList_WhenItHasNoItems()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid itemGroupId = await InsertList(db, OneDay, settingChangedAt: now.AddDays(-10));

        Assert.Empty(await DeleteDue(db));
        Assert.NotNull(await db.LoadItemGroupName(itemGroupId));
    }

    [Fact]
    public async Task DeleteDueForAutoDelete_KeepsTheList_WhenAutoDeleteIsOff()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid itemGroupId = await InsertList(
            db,
            autoDeleteAfterMinutes: null,
            settingChangedAt: null,
            now.AddDays(-400)
        );

        Assert.Empty(await DeleteDue(db));
        Assert.NotNull(await db.LoadItemGroupName(itemGroupId));
    }

    [Fact]
    public async Task DeleteDueForAutoDelete_StartsTheCountdownWhenTheSettingChanged_IfItemsWereCompletedEarlier()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        Guid recentlyEnabled = await InsertList(
            db,
            OneDay,
            settingChangedAt: now.AddHours(-1),
            now.AddDays(-30)
        );
        Guid enabledLongAgo = await InsertList(
            db,
            OneDay,
            settingChangedAt: now.AddDays(-2),
            now.AddDays(-30)
        );

        IReadOnlyList<Guid> deleted = await DeleteDue(db);

        Assert.Equal(enabledLongAgo, Assert.Single(deleted));
        Assert.NotNull(await db.LoadItemGroupName(recentlyEnabled));
    }

    private static Task<IReadOnlyList<Guid>> DeleteDue(SqlConnection db) =>
        new SqlItemGroupStore(db).DeleteDueForAutoDelete(
            now,
            TestContext.Current.CancellationToken
        );

    /// <summary>
    /// Inserts a list with auto-delete as given and one item per completion time, where
    /// <see langword="null"/> makes an open item.
    /// </summary>
    private static async Task<Guid> InsertList(
        SqlConnection db,
        int? autoDeleteAfterMinutes,
        DateTimeOffset? settingChangedAt,
        params DateTimeOffset?[] completedAt
    )
    {
        var itemGroupId = Guid.NewGuid();
        await db.InsertItemGroup(itemGroupId);
        await db.SetItemGroupSettings(
            itemGroupId,
            new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = autoDeleteAfterMinutes,
                AutoDeleteUpdatedAt = settingChangedAt,
            }
        );
        foreach (DateTimeOffset? at in completedAt)
        {
            await db.InsertItem(itemGroupId, Guid.NewGuid(), completedAt: at);
        }

        return itemGroupId;
    }
}

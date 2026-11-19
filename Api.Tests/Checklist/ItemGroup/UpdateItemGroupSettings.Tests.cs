using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class UpdateItemGroupSettingsTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_SavesTheSettings_AndSendsThemAsStored()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);

        ItemGroupSettings? settings = await SettingsUseCase(db, notifier)
            .Execute(
                new Caller(userId, "conn-settings-001"),
                itemGroupId,
                TestHelpers.ValidSettings(2880, 60),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(
            new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = 2880,
                HideCompletedAfterMinutes = 60,
                AutoDeleteUpdatedAt = now,
            },
            settings
        );
        Assert.Equal(settings, await db.LoadSettings(itemGroupId));
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupSettingsChangedNotification settingsChanged =
            Assert.IsType<CapturingNotifier.GroupSettingsChangedNotification>(notification);
        Assert.Equal(itemGroupId, settingsChanged.GroupId);
        Assert.Equal(settings, settingsChanged.Settings);
        Assert.Equal("conn-settings-001", settingsChanged.ExcludeConnectionId);
    }

    [Fact]
    public async Task Execute_ReturnsNull_AndChangesNothing_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());

        ItemGroupSettings? settings = await SettingsUseCase(db, notifier)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                TestHelpers.ValidSettings(2880, 60),
                TestContext.Current.CancellationToken
            );

        Assert.Null(settings);
        Assert.Equal(new ItemGroupSettings(), await db.LoadSettings(itemGroupId));
        Assert.Empty(notifier.Notifications);
    }

    private static UpdateItemGroupSettings SettingsUseCase(
        SqlConnection db,
        CapturingNotifier notifier
    ) =>
        new(new SqlMembership(db), new SqlItemGroupStore(db), notifier, new FixedTimeProvider(now));
}

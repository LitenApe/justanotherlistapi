using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class UpdateItemGroupSettingsTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_SavesSettingsAndNotifies_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        ClaimsPrincipal claimsPrincipal = TestHelpers.CreatePrincipal(userId);
        var notifier = new CapturingNotifier();

        await using SqlConnection db = await TestDatabase.CreateAsync();
        await InsertGroupWithMember(db, itemGroupId, userId);

        var request = new UpdateItemGroupSettings.Request
        {
            AutoDeleteAfterMinutes = 2880,
            HideCompletedAfterMinutes = 60,
        };

        // Act
        Results<
            Ok<ItemGroupSettings>,
            BadRequest,
            UnauthorizedHttpResult,
            ForbidHttpResult
        > result = await UpdateItemGroupSettings.Execute(
            itemGroupId,
            request,
            claimsPrincipal,
            db,
            notifier,
            TestHelpers.CreateHttpRequest(),
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        // Assert
        Ok<ItemGroupSettings> ok = Assert.IsType<Ok<ItemGroupSettings>>(result.Result);
        Assert.NotNull(ok.Value);
        Assert.Equal(2880, ok.Value.AutoDeleteAfterMinutes);
        Assert.Equal(60, ok.Value.HideCompletedAfterMinutes);
        Assert.NotNull(ok.Value.AutoDeleteUpdatedAt);

        ItemGroupSettings stored = await LoadSettings(db, itemGroupId);
        Assert.Equal(ok.Value, stored);

        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupSettingsChangedNotification settingsChanged =
            Assert.IsType<CapturingNotifier.GroupSettingsChangedNotification>(notification);
        Assert.Equal(itemGroupId, settingsChanged.GroupId);
        Assert.Equal(ok.Value, settingsChanged.Settings);
        Assert.Null(settingsChanged.ExcludeConnectionId);
    }

    [Theory]
    [InlineData(1439, null)]
    [InlineData(525601, null)]
    [InlineData(0, null)]
    [InlineData(null, -1)]
    [InlineData(null, 525601)]
    public async Task Execute_ReturnsBadRequest_WhenValueIsOutOfRange(
        int? autoDeleteAfterMinutes,
        int? hideCompletedAfterMinutes
    )
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();

        await using SqlConnection db = await TestDatabase.CreateAsync();
        await InsertGroupWithMember(db, itemGroupId, userId);

        var request = new UpdateItemGroupSettings.Request
        {
            AutoDeleteAfterMinutes = autoDeleteAfterMinutes,
            HideCompletedAfterMinutes = hideCompletedAfterMinutes,
        };

        // Act
        Results<
            Ok<ItemGroupSettings>,
            BadRequest,
            UnauthorizedHttpResult,
            ForbidHttpResult
        > result = await UpdateItemGroupSettings.Execute(
            itemGroupId,
            request,
            TestHelpers.CreatePrincipal(userId),
            db,
            notifier,
            TestHelpers.CreateHttpRequest(),
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<BadRequest>(result.Result);
        Assert.Empty(notifier.Notifications);
        Assert.Equal(new ItemGroupSettings(), await LoadSettings(db, itemGroupId));
    }

    [Theory]
    [InlineData(1440, 0)]
    [InlineData(525600, 525600)]
    [InlineData(null, null)]
    public async Task Execute_ReturnsOk_ForBoundaryValues(
        int? autoDeleteAfterMinutes,
        int? hideCompletedAfterMinutes
    )
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();

        await using SqlConnection db = await TestDatabase.CreateAsync();
        await InsertGroupWithMember(db, itemGroupId, userId);

        var request = new UpdateItemGroupSettings.Request
        {
            AutoDeleteAfterMinutes = autoDeleteAfterMinutes,
            HideCompletedAfterMinutes = hideCompletedAfterMinutes,
        };

        // Act
        Results<
            Ok<ItemGroupSettings>,
            BadRequest,
            UnauthorizedHttpResult,
            ForbidHttpResult
        > result = await UpdateItemGroupSettings.Execute(
            itemGroupId,
            request,
            TestHelpers.CreatePrincipal(userId),
            db,
            new CapturingNotifier(),
            TestHelpers.CreateHttpRequest(),
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        // Assert
        Ok<ItemGroupSettings> ok = Assert.IsType<Ok<ItemGroupSettings>>(result.Result);
        Assert.Equal(autoDeleteAfterMinutes, ok.Value?.AutoDeleteAfterMinutes);
        Assert.Equal(hideCompletedAfterMinutes, ok.Value?.HideCompletedAfterMinutes);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using SqlConnection db = await TestDatabase.CreateAsync();

        // Act
        Results<
            Ok<ItemGroupSettings>,
            BadRequest,
            UnauthorizedHttpResult,
            ForbidHttpResult
        > result = await UpdateItemGroupSettings.Execute(
            itemGroupId,
            new UpdateItemGroupSettings.Request(),
            claimsPrincipal,
            db,
            new CapturingNotifier(),
            TestHelpers.CreateHttpRequest(),
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();

        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        Results<
            Ok<ItemGroupSettings>,
            BadRequest,
            UnauthorizedHttpResult,
            ForbidHttpResult
        > result = await UpdateItemGroupSettings.Execute(
            itemGroupId,
            new UpdateItemGroupSettings.Request { HideCompletedAfterMinutes = 0 },
            TestHelpers.CreatePrincipal(Guid.NewGuid()),
            db,
            new CapturingNotifier(),
            TestHelpers.CreateHttpRequest(),
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<ForbidHttpResult>(result.Result);
        Assert.Equal(new ItemGroupSettings(), await LoadSettings(db, itemGroupId));
    }

    [Fact]
    public async Task Execute_PassesSignalRConnectionId_WhenHeaderIsPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        var notifier = new CapturingNotifier();

        await using SqlConnection db = await TestDatabase.CreateAsync();
        await InsertGroupWithMember(db, itemGroupId, userId);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest();
        httpRequest.Headers["X-SignalR-Connection-Id"] = "conn-settings-001";

        // Act
        await UpdateItemGroupSettings.Execute(
            itemGroupId,
            new UpdateItemGroupSettings.Request { HideCompletedAfterMinutes = 0 },
            TestHelpers.CreatePrincipal(userId),
            db,
            notifier,
            httpRequest,
            TimeProvider.System,
            TestContext.Current.CancellationToken
        );

        // Assert
        object notification = Assert.Single(notifier.Notifications);
        CapturingNotifier.GroupSettingsChangedNotification settingsChanged =
            Assert.IsType<CapturingNotifier.GroupSettingsChangedNotification>(notification);
        Assert.Equal("conn-settings-001", settingsChanged.ExcludeConnectionId);
    }

    [Fact]
    public async Task UpdateData_RestartsAutoDeleteCountdown_OnlyWhenAutoDeleteChanges()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await InsertGroupWithMember(db, itemGroupId, Guid.NewGuid());
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act & Assert: turning auto-delete on starts the countdown.
        ItemGroupSettings enabled = await UpdateItemGroupSettings.UpdateData(
            itemGroupId,
            new UpdateItemGroupSettings.Request { AutoDeleteAfterMinutes = 1440 },
            now,
            db,
            ct
        );
        Assert.Equal(now, enabled.AutoDeleteUpdatedAt);

        // Changing only the hide setting keeps it.
        ItemGroupSettings hideChanged = await UpdateItemGroupSettings.UpdateData(
            itemGroupId,
            new UpdateItemGroupSettings.Request
            {
                AutoDeleteAfterMinutes = 1440,
                HideCompletedAfterMinutes = 30,
            },
            now.AddHours(1),
            db,
            ct
        );
        Assert.Equal(now, hideChanged.AutoDeleteUpdatedAt);
        Assert.Equal(30, hideChanged.HideCompletedAfterMinutes);

        // Changing the delay restarts it.
        ItemGroupSettings delayChanged = await UpdateItemGroupSettings.UpdateData(
            itemGroupId,
            new UpdateItemGroupSettings.Request
            {
                AutoDeleteAfterMinutes = 2880,
                HideCompletedAfterMinutes = 30,
            },
            now.AddHours(2),
            db,
            ct
        );
        Assert.Equal(now.AddHours(2), delayChanged.AutoDeleteUpdatedAt);

        // Turning it off is a change too.
        ItemGroupSettings disabled = await UpdateItemGroupSettings.UpdateData(
            itemGroupId,
            new UpdateItemGroupSettings.Request(),
            now.AddHours(3),
            db,
            ct
        );
        Assert.Null(disabled.AutoDeleteAfterMinutes);
        Assert.Null(disabled.HideCompletedAfterMinutes);
        Assert.Equal(now.AddHours(3), disabled.AutoDeleteUpdatedAt);
    }

    private static async Task InsertGroupWithMember(
        SqlConnection db,
        Guid itemGroupId,
        Guid memberId
    )
    {
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = memberId, ItemGroupId = itemGroupId }
        );
    }

    private static Task<ItemGroupSettings> LoadSettings(SqlConnection db, Guid itemGroupId) =>
        db.QuerySingleAsync<ItemGroupSettings>(
            "SELECT AutoDeleteAfterMinutes, HideCompletedAfterMinutes, AutoDeleteUpdatedAt FROM ItemGroups WHERE Id = @Id",
            new { Id = itemGroupId }
        );
}

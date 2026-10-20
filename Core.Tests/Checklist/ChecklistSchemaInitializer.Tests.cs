using Core.Checklist;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Core.Tests.Checklist;

public sealed class ChecklistSchemaInitializerTests
{
    [Fact]
    public async Task CreateSchemaAsync_AddsSettingsColumns_ToDatabasesCreatedBeforeThem()
    {
        // Arrange: the schema as it was before list settings existed.
        await using SqlConnection db = await TestDatabase.CreateEmptyAsync();
        await db.ExecuteAsync(
            """
            CREATE TABLE ItemGroups (
                Id   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                Name NVARCHAR(MAX)    NOT NULL
            );
            CREATE TABLE Items (
                Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                Name        NVARCHAR(MAX)    NOT NULL,
                Description NVARCHAR(MAX)    NULL,
                IsComplete  BIT              NOT NULL DEFAULT 0,
                ItemGroupId UNIQUEIDENTIFIER NOT NULL,
                FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
            );
            """
        );
        var groupId = Guid.NewGuid();
        var doneId = Guid.NewGuid();
        var openId = Guid.NewGuid();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, 'Old list')",
            new { Id = groupId }
        );
        await db.ExecuteAsync(
            """
            INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES
                (@DoneId, 'Done', 1, @GroupId),
                (@OpenId, 'Open', 0, @GroupId)
            """,
            new
            {
                DoneId = doneId,
                OpenId = openId,
                GroupId = groupId,
            }
        );
        DateTimeOffset before = DateTimeOffset.UtcNow.AddMinutes(-1);

        // Act: run twice to confirm the upgrade is idempotent.
        await ChecklistSchemaInitializer.CreateSchemaAsync(
            db,
            TestContext.Current.CancellationToken
        );
        await ChecklistSchemaInitializer.CreateSchemaAsync(
            db,
            TestContext.Current.CancellationToken
        );

        // Assert: settings default to off, and completed items start their clock now.
        ItemGroupSettings settings = await db.QuerySingleAsync<ItemGroupSettings>(
            "SELECT AutoDeleteAfterMinutes, HideCompletedAfterMinutes, AutoDeleteUpdatedAt FROM ItemGroups WHERE Id = @Id",
            new { Id = groupId }
        );
        Assert.Equal(new ItemGroupSettings(), settings);

        DateTimeOffset? doneAt = await db.ExecuteScalarAsync<DateTimeOffset?>(
            "SELECT CompletedAt FROM Items WHERE Id = @Id",
            new { Id = doneId }
        );
        Assert.NotNull(doneAt);
        Assert.True(
            doneAt >= before,
            $"Expected the backfill to use the current time, got {doneAt}."
        );

        Assert.Null(
            await db.ExecuteScalarAsync<DateTimeOffset?>(
                "SELECT CompletedAt FROM Items WHERE Id = @Id",
                new { Id = openId }
            )
        );
    }
}

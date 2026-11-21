using Api.Sql;
using Checklist.Domain;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Sql;

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
        Assert.Equal(new ItemGroupSettings(), await db.LoadSettings(groupId));

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

    [Fact]
    public async Task CreateSchemaAsync_AddsReservationColumns_ToDatabasesCreatedBeforeThem()
    {
        // Arrange: the schema as it was before reservations existed.
        await using SqlConnection db = await TestDatabase.CreateEmptyAsync();
        await db.ExecuteAsync(
            """
            CREATE TABLE ItemGroups (
                Id                        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                Name                      NVARCHAR(MAX)    NOT NULL,
                AutoDeleteAfterMinutes    INT              NULL,
                HideCompletedAfterMinutes INT              NULL,
                AutoDeleteUpdatedAt       DATETIMEOFFSET   NULL
            );
            CREATE TABLE Items (
                Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                Name        NVARCHAR(MAX)    NOT NULL,
                Description NVARCHAR(MAX)    NULL,
                IsComplete  BIT              NOT NULL DEFAULT 0,
                CompletedAt DATETIMEOFFSET   NULL,
                ItemGroupId UNIQUEIDENTIFIER NOT NULL,
                FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
            );
            """
        );
        var groupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await db.ExecuteAsync(
            """
            INSERT INTO ItemGroups (Id, Name) VALUES (@GroupId, 'Old list');
            INSERT INTO Items (Id, Name, ItemGroupId) VALUES (@ItemId, 'Tent', @GroupId);
            """,
            new { GroupId = groupId, ItemId = itemId }
        );

        // Act: run twice to confirm the upgrade is idempotent.
        await ChecklistSchemaInitializer.CreateSchemaAsync(
            db,
            TestContext.Current.CancellationToken
        );
        await ChecklistSchemaInitializer.CreateSchemaAsync(
            db,
            TestContext.Current.CancellationToken
        );

        // Assert: existing items start free, reservations are kept whole, and lists don't
        // protect them.
        Item? item = await db.LoadItem(itemId);
        Assert.NotNull(item);
        Assert.Null(item.ReservedFor);
        Assert.Null(item.ReservedBy);
        await AssertRejectsHalfAReservation(db, itemId);
        Assert.False((await db.LoadSettings(groupId))?.ProtectReservations);
    }

    [Fact]
    public async Task CreateSchemaAsync_KeepsReservationsWhole()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId);
        await db.InsertItem(itemGroupId, itemId);

        await AssertRejectsHalfAReservation(db, itemId);
    }

    [Fact]
    public async Task CreateSchemaAsync_AddsTheUsersTable_ToDatabasesCreatedBeforeAccounts()
    {
        // Arrange: the schema as it was before accounts existed.
        await using SqlConnection db = await TestDatabase.CreateEmptyAsync();
        await db.ExecuteAsync(
            """
            CREATE TABLE ItemGroups (Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, Name NVARCHAR(MAX) NOT NULL);
            CREATE TABLE Members (
                MemberId    UNIQUEIDENTIFIER NOT NULL,
                ItemGroupId UNIQUEIDENTIFIER NOT NULL,
                PRIMARY KEY (MemberId, ItemGroupId)
            );
            """
        );

        // Act: run twice to confirm the upgrade is idempotent.
        await ChecklistSchemaInitializer.CreateSchemaAsync(
            db,
            TestContext.Current.CancellationToken
        );
        await ChecklistSchemaInitializer.CreateSchemaAsync(
            db,
            TestContext.Current.CancellationToken
        );

        // Assert: the table is there, and holds one account per email, ignoring case.
        const string insert = """
            INSERT INTO Users (Id, Name, Email, NormalizedEmail, PasswordHash, SecurityStamp, CreatedAt)
            VALUES (NEWID(), 'Kim', @Email, UPPER(@Email), 'hash', 'STAMP', SYSDATETIMEOFFSET())
            """;
        await db.ExecuteAsync(insert, new { Email = "kim@example.com" });
        SqlException duplicate = await Assert.ThrowsAsync<SqlException>(() =>
            db.ExecuteAsync(insert, new { Email = "KIM@example.com" })
        );
        Assert.Contains("UX_Users_NormalizedEmail", duplicate.Message, StringComparison.Ordinal);
    }

    /// <summary>An item is reserved for someone by someone, or free: never one without the other.</summary>
    private static async Task AssertRejectsHalfAReservation(SqlConnection db, Guid itemId)
    {
        string[] halves =
        [
            "UPDATE Items SET ReservedFor = NEWID() WHERE Id = @Id",
            "UPDATE Items SET ReservedBy = NEWID() WHERE Id = @Id",
        ];
        foreach (string half in halves)
        {
            SqlException error = await Assert.ThrowsAsync<SqlException>(() =>
                db.ExecuteAsync(half, new { Id = itemId })
            );
            Assert.Contains("CK_Items_Reservation", error.Message, StringComparison.Ordinal);
        }
    }
}

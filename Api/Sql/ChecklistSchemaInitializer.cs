using Dapper;
using Microsoft.Data.SqlClient;

namespace Api.Sql;

internal static class ChecklistSchemaInitializer
{
    /// <summary>The tables as they are now, for new databases.</summary>
    private const string CreateTables = """
        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemGroups' AND schema_id = SCHEMA_ID('dbo'))
        CREATE TABLE ItemGroups (
            Id                        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            Name                      NVARCHAR(MAX)    NOT NULL,
            AutoDeleteAfterMinutes    INT              NULL,
            HideCompletedAfterMinutes INT              NULL,
            AutoDeleteUpdatedAt       DATETIMEOFFSET   NULL,
            ProtectReservations       BIT              NOT NULL
                CONSTRAINT DF_ItemGroups_ProtectReservations DEFAULT 0
        );

        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Items' AND schema_id = SCHEMA_ID('dbo'))
        CREATE TABLE Items (
            Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            Name        NVARCHAR(MAX)    NOT NULL,
            Description NVARCHAR(MAX)    NULL,
            IsComplete  BIT              NOT NULL DEFAULT 0,
            CompletedAt DATETIMEOFFSET   NULL,
            ItemGroupId UNIQUEIDENTIFIER NOT NULL,
            ReservedFor UNIQUEIDENTIFIER NULL,
            ReservedBy  UNIQUEIDENTIFIER NULL,
            FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE,
            CONSTRAINT CK_Items_Reservation CHECK (
                (ReservedFor IS NULL AND ReservedBy IS NULL)
                OR (ReservedFor IS NOT NULL AND ReservedBy IS NOT NULL)
            )
        );

        IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Members' AND schema_id = SCHEMA_ID('dbo'))
        CREATE TABLE Members (
            MemberId    UNIQUEIDENTIFIER NOT NULL,
            ItemGroupId UNIQUEIDENTIFIER NOT NULL,
            PRIMARY KEY (MemberId, ItemGroupId),
            FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
        );
        """;

    /// <summary>Brings tables created by earlier versions up to date, column by column.</summary>
    private const string UpgradeTables = """
        -- Upgrade databases created before list settings existed.
        IF COL_LENGTH('dbo.ItemGroups', 'AutoDeleteAfterMinutes') IS NULL
        ALTER TABLE ItemGroups ADD
            AutoDeleteAfterMinutes    INT            NULL,
            HideCompletedAfterMinutes INT            NULL,
            AutoDeleteUpdatedAt       DATETIMEOFFSET NULL;

        IF COL_LENGTH('dbo.Items', 'CompletedAt') IS NULL
        BEGIN
            ALTER TABLE Items ADD CompletedAt DATETIMEOFFSET NULL;
            -- Items completed before the column existed start their clock now.
            -- EXEC defers compilation until the new column exists.
            EXEC('UPDATE Items SET CompletedAt = SYSDATETIMEOFFSET() WHERE IsComplete = 1');
        END

        -- Upgrade databases created before reservations existed. Every item starts free,
        -- and every list leaves reservations open to all its members.
        IF COL_LENGTH('dbo.ItemGroups', 'ProtectReservations') IS NULL
        ALTER TABLE ItemGroups ADD ProtectReservations BIT NOT NULL
            CONSTRAINT DF_ItemGroups_ProtectReservations DEFAULT 0;

        IF COL_LENGTH('dbo.Items', 'ReservedFor') IS NULL
        BEGIN
            ALTER TABLE Items ADD
                ReservedFor UNIQUEIDENTIFIER NULL,
                ReservedBy  UNIQUEIDENTIFIER NULL;
            -- EXEC defers compilation until the new columns exist.
            EXEC('
                ALTER TABLE Items ADD CONSTRAINT CK_Items_Reservation CHECK (
                    (ReservedFor IS NULL AND ReservedBy IS NULL)
                    OR (ReservedFor IS NOT NULL AND ReservedBy IS NOT NULL)
                )
            ');
        END
        """;

    private const string CreateIndexes = """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Items_ItemGroupId' AND object_id = OBJECT_ID('dbo.Items'))
            CREATE INDEX IX_Items_ItemGroupId ON Items(ItemGroupId);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_ItemGroupId' AND object_id = OBJECT_ID('dbo.Members'))
            CREATE INDEX IX_Members_ItemGroupId ON Members(ItemGroupId);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_MemberId_ItemGroupId' AND object_id = OBJECT_ID('dbo.Members'))
            CREATE INDEX IX_Members_MemberId_ItemGroupId ON Members(MemberId, ItemGroupId);
        """;

    internal static async Task CreateSchemaAsync(
        SqlConnection connection,
        CancellationToken ct = default
    )
    {
        foreach (string batch in new[] { CreateTables, UpgradeTables, CreateIndexes })
        {
            await connection.ExecuteAsync(new CommandDefinition(batch, cancellationToken: ct));
        }
    }
}

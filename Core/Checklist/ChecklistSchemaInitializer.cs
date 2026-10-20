using Dapper;
using Microsoft.Data.SqlClient;

namespace Core.Checklist;

internal static class ChecklistSchemaInitializer
{
    internal static async Task CreateSchemaAsync(
        SqlConnection connection,
        CancellationToken ct = default
    )
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemGroups' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE ItemGroups (
                    Id                        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    Name                      NVARCHAR(MAX)    NOT NULL,
                    AutoDeleteAfterMinutes    INT              NULL,
                    HideCompletedAfterMinutes INT              NULL,
                    AutoDeleteUpdatedAt       DATETIMEOFFSET   NULL
                );

                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Items' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE Items (
                    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    Name        NVARCHAR(MAX)    NOT NULL,
                    Description NVARCHAR(MAX)    NULL,
                    IsComplete  BIT              NOT NULL DEFAULT 0,
                    CompletedAt DATETIMEOFFSET   NULL,
                    ItemGroupId UNIQUEIDENTIFIER NOT NULL,
                    FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
                );

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

                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Members' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE Members (
                    MemberId    UNIQUEIDENTIFIER NOT NULL,
                    ItemGroupId UNIQUEIDENTIFIER NOT NULL,
                    PRIMARY KEY (MemberId, ItemGroupId),
                    FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Items_ItemGroupId' AND object_id = OBJECT_ID('dbo.Items'))
                    CREATE INDEX IX_Items_ItemGroupId ON Items(ItemGroupId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_ItemGroupId' AND object_id = OBJECT_ID('dbo.Members'))
                    CREATE INDEX IX_Members_ItemGroupId ON Members(ItemGroupId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Members_MemberId_ItemGroupId' AND object_id = OBJECT_ID('dbo.Members'))
                    CREATE INDEX IX_Members_MemberId_ItemGroupId ON Members(MemberId, ItemGroupId);
                """,
                cancellationToken: ct
            )
        );
    }
}

using Dapper;
using Microsoft.Data.SqlClient;

namespace Core;

internal static class DatabaseInitializer
{
    internal static async Task InitializeAsync(
        SqlConnection connection,
        CancellationToken ct = default
    )
    {
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ItemGroups' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE ItemGroups (
                    Id   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    Name NVARCHAR(MAX)    NOT NULL
                );

                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Items' AND schema_id = SCHEMA_ID('dbo'))
                CREATE TABLE Items (
                    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    Name        NVARCHAR(MAX)    NOT NULL,
                    Description NVARCHAR(MAX)    NULL,
                    IsComplete  BIT              NOT NULL DEFAULT 0,
                    ItemGroupId UNIQUEIDENTIFIER NOT NULL,
                    FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
                );

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

using Dapper;
using Microsoft.Data.SqlClient;

namespace Core.AuditLog;

internal static class AuditLogSchemaInitializer
{
    internal static async Task CreateSchemaAsync(
        SqlConnection connection,
        CancellationToken ct = default
    )
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditLog' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    CREATE TABLE AuditLog (
                        Id              UNIQUEIDENTIFIER  NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
                        Timestamp       DATETIMEOFFSET    NOT NULL,
                        TraceId         NVARCHAR(32)      NULL,
                        UserId          UNIQUEIDENTIFIER  NULL,
                        IpAddress       NVARCHAR(45)      NULL,
                        ResourceType    NVARCHAR(20)      NULL,
                        Operation       NVARCHAR(50)      NOT NULL,
                        ResourceId      UNIQUEIDENTIFIER  NULL,
                        SubResourceId   UNIQUEIDENTIFIER  NULL,
                        TargetUserId    UNIQUEIDENTIFIER  NULL,
                        Outcome         NVARCHAR(32)      NOT NULL,
                        FailureReason   NVARCHAR(500)     NULL
                    );

                    CREATE INDEX IX_AuditLog_UserId_Timestamp        ON AuditLog (UserId,       Timestamp DESC);
                    CREATE INDEX IX_AuditLog_ResourceId_Timestamp    ON AuditLog (ResourceId,   Timestamp DESC);
                    CREATE INDEX IX_AuditLog_ResourceType_Timestamp  ON AuditLog (ResourceType, Timestamp DESC);
                    CREATE INDEX IX_AuditLog_Outcome_Timestamp       ON AuditLog (Outcome,      Timestamp DESC);
                    CREATE INDEX IX_AuditLog_Timestamp               ON AuditLog (Timestamp     DESC);
                END
                """,
                cancellationToken: ct
            )
        );
    }
}

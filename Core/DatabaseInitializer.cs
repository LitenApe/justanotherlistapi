using Core.AuditLog;
using Core.Checklist;
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
        await ChecklistSchemaInitializer.CreateSchemaAsync(connection, ct);
        await AuditLogSchemaInitializer.CreateSchemaAsync(connection, ct);
    }
}

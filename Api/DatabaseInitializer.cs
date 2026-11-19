using Api.AuditLog;
using Api.Checklist;
using Microsoft.Data.SqlClient;

namespace Api;

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

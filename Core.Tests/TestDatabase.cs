using Core.AuditLog;
using Core.Checklist;
using Dapper;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Core.Tests;

/// <summary>
/// Creates a SQL Server database (via Testcontainers) with the application schema applied,
/// for use in unit and integration tests. One container is shared for the entire test run;
/// each call to <see cref="CreateAsync"/> creates an isolated, uniquely-named database.
/// </summary>
internal static class TestDatabase
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static MsSqlContainer? container;

    private static async Task<string> GetContainerConnectionStringAsync()
    {
        if (container is not null)
        {
            return container.GetConnectionString();
        }

        await InitLock.WaitAsync();
        try
        {
            if (container is null)
            {
                container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
                await container.StartAsync();
            }
        }
        finally
        {
            InitLock.Release();
        }

        return container.GetConnectionString();
    }

    public static async Task<SqlConnection> CreateAsync()
    {
        return await OpenAsync(await CreateConnectionStringAsync());
    }

    /// <summary>
    /// Creates an isolated database with the application schema and returns its connection
    /// string, for an app under test that opens its own connections.
    /// </summary>
    public static async Task<string> CreateConnectionStringAsync()
    {
        string connectionString = await CreateEmptyDatabaseAsync();
        await using SqlConnection connection = await OpenAsync(connectionString);
        await ChecklistSchemaInitializer.CreateSchemaAsync(connection);
        await AuditLogSchemaInitializer.CreateSchemaAsync(connection);
        return connectionString;
    }

    /// <summary>Creates an isolated database without the application schema.</summary>
    public static async Task<SqlConnection> CreateEmptyAsync()
    {
        return await OpenAsync(await CreateEmptyDatabaseAsync());
    }

    private static async Task<string> CreateEmptyDatabaseAsync()
    {
        string masterConnectionString = await GetContainerConnectionStringAsync();
        string dbName = $"test_{Guid.NewGuid():N}";

        await using (SqlConnection master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync();
            await master.ExecuteAsync($"CREATE DATABASE [{dbName}]");
        }

        return new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = dbName,
        }.ConnectionString;
    }

    private static async Task<SqlConnection> OpenAsync(string connectionString)
    {
        SqlConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }
}

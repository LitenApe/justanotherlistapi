using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Core.Tests;

/// <summary>
/// Creates an in-memory SQLite connection with the application schema set up,
/// for use in unit tests as a substitute for a real SQL Server connection.
/// </summary>
internal static class TestDatabase
{
    static TestDatabase()
    {
        // Register a type handler so Dapper maps Guid ↔ TEXT correctly for SQLite.
        // This is process-scoped to the test runner and does not affect production.
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
    }

    public static async Task<SqliteConnection> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateTablesAsync(connection);
        return connection;
    }

    private static async Task CreateTablesAsync(SqliteConnection connection)
    {
        await connection.ExecuteAsync(
            """
            CREATE TABLE ItemGroups (
                Id   TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE Items (
                Id          TEXT    NOT NULL PRIMARY KEY,
                Name        TEXT    NOT NULL,
                Description TEXT    NULL,
                IsComplete  INTEGER NOT NULL DEFAULT 0,
                ItemGroupId TEXT    NOT NULL REFERENCES ItemGroups(Id) ON DELETE CASCADE
            );

            CREATE TABLE Members (
                MemberId    TEXT NOT NULL,
                ItemGroupId TEXT NOT NULL,
                PRIMARY KEY (MemberId, ItemGroupId),
                FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE CASCADE
            );
            """);
    }
}

internal class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
        => parameter.Value = value.ToString();

    public override Guid Parse(object value)
        => Guid.Parse((string)value);
}

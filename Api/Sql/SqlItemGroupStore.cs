using System.Data;
using Checklist.Domain;
using Checklist.Ports;
using Dapper;

namespace Api.Sql;

/// <summary>
/// <see cref="IItemGroupStore"/> on SQL Server: the <c>ItemGroups</c> table, read together with
/// the <c>Items</c> and <c>Members</c> that belong to each list.
/// </summary>
internal sealed class SqlItemGroupStore(IDbConnection db) : IItemGroupStore
{
    /// <inheritdoc/>
    /// <remarks>One transaction, so a list never exists without its first member.</remarks>
    public async Task Create(Guid itemGroupId, Name name, Guid firstMemberId, CancellationToken ct)
    {
        // Dapper opens connections per command, but a transaction needs one that is already open.
        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        using IDbTransaction tx = db.BeginTransaction();

        await db.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name);
                INSERT INTO Members (ItemGroupId, MemberId) VALUES (@Id, @MemberId);
                """,
                new
                {
                    Id = itemGroupId,
                    Name = name.Value,
                    MemberId = firstMemberId,
                },
                transaction: tx,
                cancellationToken: ct
            )
        );

        tx.Commit();
    }

    public async Task<ItemGroup?> Load(Guid itemGroupId, CancellationToken ct)
    {
        await using SqlMapper.GridReader multi = await db.QueryMultipleAsync(
            new CommandDefinition(
                """
                SELECT Id, Name, AutoDeleteAfterMinutes, HideCompletedAfterMinutes, AutoDeleteUpdatedAt
                FROM ItemGroups WHERE Id = @Id;
                SELECT Id, Name, Description, IsComplete, CompletedAt, ItemGroupId FROM Items WHERE ItemGroupId = @Id;
                SELECT MemberId FROM Members WHERE ItemGroupId = @Id;
                """,
                new { Id = itemGroupId },
                cancellationToken: ct
            )
        );

        ItemGroupRow? row = await multi.ReadFirstOrDefaultAsync<ItemGroupRow>();
        if (row is null)
        {
            return null;
        }

        IEnumerable<Item> items = await multi.ReadAsync<Item>();
        IEnumerable<Guid> members = await multi.ReadAsync<Guid>();

        return row.ToItemGroup() with
        {
            Items = items.ToList(),
            Members = members.ToList(),
        };
    }

    public async Task<IReadOnlyList<ItemGroup>> ListForMember(Guid memberId, CancellationToken ct)
    {
        await using SqlMapper.GridReader multi = await db.QueryMultipleAsync(
            new CommandDefinition(
                """
                SELECT ig.Id, ig.Name, ig.AutoDeleteAfterMinutes, ig.HideCompletedAfterMinutes, ig.AutoDeleteUpdatedAt
                FROM ItemGroups ig
                INNER JOIN Members m ON m.ItemGroupId = ig.Id
                WHERE m.MemberId = @MemberId;

                SELECT i.Id, i.Name, i.Description, i.IsComplete, i.CompletedAt, i.ItemGroupId
                FROM Items i
                INNER JOIN Members m ON m.ItemGroupId = i.ItemGroupId
                WHERE m.MemberId = @MemberId AND i.IsComplete = 0;
                """,
                new { MemberId = memberId },
                cancellationToken: ct
            )
        );

        IEnumerable<ItemGroupRow> rows = await multi.ReadAsync<ItemGroupRow>();
        ILookup<Guid, Item> openItems = (await multi.ReadAsync<Item>()).ToLookup(i =>
            i.ItemGroupId
        );

        return [.. rows.Select(row => row.ToItemGroup() with { Items = [.. openItems[row.Id]] })];
    }

    public async Task Rename(Guid itemGroupId, Name name, CancellationToken ct)
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "UPDATE ItemGroups SET Name = @Name WHERE Id = @Id",
                new { Id = itemGroupId, Name = name.Value },
                cancellationToken: ct
            )
        );
    }

    /// <inheritdoc/>
    /// <remarks>
    /// One statement that compares with the stored delay as it writes, so only a real change
    /// restarts the countdown, even when two members save at once.
    /// </remarks>
    public async Task<ItemGroupSettings?> UpdateSettings(
        Guid itemGroupId,
        ItemGroupSettingsInput settings,
        DateTimeOffset now,
        CancellationToken ct
    ) =>
        await db.QuerySingleOrDefaultAsync<ItemGroupSettings>(
            new CommandDefinition(
                """
                UPDATE ItemGroups
                SET AutoDeleteUpdatedAt = CASE
                        WHEN AutoDeleteAfterMinutes IS DISTINCT FROM @AutoDeleteAfterMinutes THEN @Now
                        ELSE AutoDeleteUpdatedAt
                    END,
                    AutoDeleteAfterMinutes = @AutoDeleteAfterMinutes,
                    HideCompletedAfterMinutes = @HideCompletedAfterMinutes
                OUTPUT INSERTED.AutoDeleteAfterMinutes, INSERTED.HideCompletedAfterMinutes, INSERTED.AutoDeleteUpdatedAt
                WHERE Id = @Id
                """,
                new
                {
                    Id = itemGroupId,
                    settings.AutoDeleteAfterMinutes,
                    settings.HideCompletedAfterMinutes,
                    Now = now,
                },
                cancellationToken: ct
            )
        );

    /// <inheritdoc/>
    /// <remarks>The foreign keys cascade to the list's items and members.</remarks>
    public async Task Delete(Guid itemGroupId, CancellationToken ct)
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM ItemGroups WHERE Id = @Id",
                new { Id = itemGroupId },
                cancellationToken: ct
            )
        );
    }

    /// <inheritdoc/>
    /// <remarks>
    /// One <c>DELETE … OUTPUT</c> statement: of two app instances running it at once, only the
    /// one that deletes a list gets its ID back, so each list is audited and announced once.
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> DeleteDueForAutoDelete(
        DateTimeOffset now,
        CancellationToken ct
    ) =>
        [
            .. await db.QueryAsync<Guid>(
                new CommandDefinition(
                    """
                    DELETE ig
                    OUTPUT DELETED.Id
                    FROM ItemGroups ig
                    CROSS APPLY (
                        SELECT
                            COUNT(*) AS ItemCount,
                            COUNT(CASE WHEN i.IsComplete = 0 THEN 1 END) AS OpenCount,
                            MAX(i.CompletedAt) AS LastCompletedAt
                        FROM Items i
                        WHERE i.ItemGroupId = ig.Id
                    ) stats
                    WHERE ig.AutoDeleteAfterMinutes IS NOT NULL
                        AND stats.ItemCount > 0
                        AND stats.OpenCount = 0
                        AND GREATEST(stats.LastCompletedAt, ig.AutoDeleteUpdatedAt)
                            <= DATEADD(MINUTE, -ig.AutoDeleteAfterMinutes, @Now);
                    """,
                    new { Now = now },
                    cancellationToken: ct
                )
            ),
        ];
}

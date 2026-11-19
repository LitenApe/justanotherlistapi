using System.Data;
using Checklist.Domain;
using Checklist.Ports;
using Dapper;

namespace Api.Sql;

/// <summary><see cref="IItemStore"/> on SQL Server: the <c>Items</c> table.</summary>
internal sealed class SqlItemStore(IDbConnection db) : IItemStore
{
    public async Task Create(Item item, CancellationToken ct)
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO Items (Id, Name, Description, IsComplete, CompletedAt, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @CompletedAt, @ItemGroupId)",
                new
                {
                    item.Id,
                    item.Name,
                    item.Description,
                    item.IsComplete,
                    item.CompletedAt,
                    item.ItemGroupId,
                },
                cancellationToken: ct
            )
        );
    }

    /// <inheritdoc/>
    /// <remarks>
    /// One statement that reads the stored completion time as it writes, so two members
    /// completing the same item at once keep the first one's time.
    /// </remarks>
    public Task<Item?> Update(
        Guid itemGroupId,
        Guid itemId,
        ItemInput input,
        DateTimeOffset now,
        CancellationToken ct
    ) =>
        db.QuerySingleOrDefaultAsync<Item>(
            new CommandDefinition(
                """
                UPDATE Items
                SET Name = @Name,
                    Description = @Description,
                    IsComplete = @IsComplete,
                    CompletedAt = CASE WHEN @IsComplete = 1 THEN COALESCE(CompletedAt, @Now) END
                OUTPUT INSERTED.Id, INSERTED.Name, INSERTED.Description, INSERTED.IsComplete,
                    INSERTED.CompletedAt, INSERTED.ItemGroupId
                WHERE Id = @Id AND ItemGroupId = @ItemGroupId
                """,
                new
                {
                    Id = itemId,
                    ItemGroupId = itemGroupId,
                    Name = input.Name.Value,
                    input.Description,
                    input.IsComplete,
                    Now = now,
                },
                cancellationToken: ct
            )
        );

    public async Task Delete(Guid itemGroupId, Guid itemId, CancellationToken ct)
    {
        await db.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Items WHERE Id = @Id AND ItemGroupId = @ItemGroupId",
                new { Id = itemId, ItemGroupId = itemGroupId },
                cancellationToken: ct
            )
        );
    }
}

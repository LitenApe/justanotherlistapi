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
                    INSERTED.CompletedAt, INSERTED.ItemGroupId, INSERTED.ReservedFor,
                    INSERTED.ReservedBy
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

    /// <inheritdoc/>
    /// <remarks>
    /// One transaction that first takes an update lock on the list's <c>ItemGroups</c> row, the
    /// lock <see cref="SqlMembership.RemoveUnlessLast"/> takes first too. So reservation
    /// changes, removals and settings changes in one list take turns, always locking in the same
    /// order, and <see cref="ReservationChange.Decide"/> sees the reservation, the member and
    /// the setting as they stay until the change is written.
    /// </remarks>
    public async Task<ReservationResult> ChangeReservation(
        Guid itemGroupId,
        Guid itemId,
        ReservationChange change,
        CancellationToken ct
    )
    {
        // Dapper opens connections per command, but a transaction needs one that is already open.
        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        using IDbTransaction tx = db.BeginTransaction();

        ReservationState? state = await db.QuerySingleOrDefaultAsync<ReservationState>(
            new CommandDefinition(
                """
                DECLARE @ProtectReservations BIT;
                SELECT @ProtectReservations = ProtectReservations
                FROM ItemGroups WITH (UPDLOCK) WHERE Id = @ItemGroupId;

                SELECT i.ReservedFor, i.ReservedBy, @ProtectReservations AS ProtectReservations,
                    CAST(CASE WHEN EXISTS (
                        SELECT 1 FROM Members m
                        WHERE m.ItemGroupId = @ItemGroupId AND m.MemberId = @MemberId
                    ) THEN 1 ELSE 0 END AS BIT) AS MemberBelongs
                FROM Items i WITH (UPDLOCK)
                WHERE i.Id = @ItemId AND i.ItemGroupId = @ItemGroupId;
                """,
                new
                {
                    ItemGroupId = itemGroupId,
                    ItemId = itemId,
                    change.MemberId,
                },
                transaction: tx,
                cancellationToken: ct
            )
        );
        if (state is null)
        {
            return new ReservationResult(ReservationDecision.Unchanged, Item: null);
        }

        ReservationDecision decision = change.Decide(
            state.ReservedFor,
            state.ReservedBy,
            state.ProtectReservations,
            state.MemberBelongs
        );
        if (decision != ReservationDecision.Change)
        {
            return new ReservationResult(decision, Item: null);
        }

        Item item = await db.QuerySingleAsync<Item>(
            new CommandDefinition(
                """
                UPDATE Items
                SET ReservedFor = @MemberId,
                    ReservedBy = CASE WHEN @MemberId IS NULL THEN NULL ELSE @CallerId END
                OUTPUT INSERTED.Id, INSERTED.Name, INSERTED.Description, INSERTED.IsComplete,
                    INSERTED.CompletedAt, INSERTED.ItemGroupId, INSERTED.ReservedFor,
                    INSERTED.ReservedBy
                WHERE Id = @ItemId AND ItemGroupId = @ItemGroupId
                """,
                new
                {
                    ItemGroupId = itemGroupId,
                    ItemId = itemId,
                    change.MemberId,
                    change.CallerId,
                },
                transaction: tx,
                cancellationToken: ct
            )
        );

        tx.Commit();
        return new ReservationResult(decision, item);
    }

    /// <summary>What <see cref="ReservationChange.Decide"/> needs to know, read under the lock.</summary>
    private sealed record ReservationState
    {
        public Guid? ReservedFor { get; init; }

        public Guid? ReservedBy { get; init; }

        public bool ProtectReservations { get; init; }

        public bool MemberBelongs { get; init; }
    }
}

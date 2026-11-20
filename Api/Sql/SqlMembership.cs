using System.Data;
using Checklist.Ports;
using Dapper;

namespace Api.Sql;

/// <summary><see cref="IMembership"/> on SQL Server: the <c>Members</c> table.</summary>
internal sealed class SqlMembership(IDbConnection db) : IMembership
{
    public async Task<bool> IsMember(Guid itemGroupId, Guid userId, CancellationToken ct)
    {
        int count = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Members WHERE ItemGroupId = @ItemGroupId AND MemberId = @MemberId",
                new { ItemGroupId = itemGroupId, MemberId = userId },
                cancellationToken: ct
            )
        );

        return count > 0;
    }

    public async Task<IReadOnlyList<Guid>> ListMembers(Guid itemGroupId, CancellationToken ct)
    {
        IEnumerable<Guid> members = await db.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT MemberId FROM Members WHERE ItemGroupId = @ItemGroupId",
                new { ItemGroupId = itemGroupId },
                cancellationToken: ct
            )
        );

        return [.. members];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// One statement whose existence check holds its locks until the insert. Of two concurrent
    /// adds of the same member, the second waits, then finds the row and returns
    /// <see langword="false"/> instead of failing on the primary key.
    /// </remarks>
    public async Task<bool> Add(Guid itemGroupId, Guid memberId, CancellationToken ct)
    {
        int inserted = await db.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Members (MemberId, ItemGroupId)
                SELECT @MemberId, @ItemGroupId
                WHERE NOT EXISTS (
                    SELECT 1 FROM Members WITH (UPDLOCK, HOLDLOCK)
                    WHERE MemberId = @MemberId AND ItemGroupId = @ItemGroupId
                );
                """,
                new { MemberId = memberId, ItemGroupId = itemGroupId },
                cancellationToken: ct
            )
        );

        return inserted == 1;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The check, the delete and freeing the member's items share a transaction that first
    /// takes an update lock on the list's <c>ItemGroups</c> row, so removals from one list take
    /// turns. Without it, two members removing each other (or themselves) at the same moment
    /// could both pass the check and leave the list with no members. Freeing the items in the
    /// same transaction means no item is ever reserved for someone outside the list.
    /// </remarks>
    public async Task<bool> RemoveUnlessLast(Guid itemGroupId, Guid memberId, CancellationToken ct)
    {
        // Dapper opens connections per command, but a transaction needs one that is already open.
        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        using IDbTransaction tx = db.BeginTransaction();

        int isLastMember = await db.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                DECLARE @Locked BIT;
                SELECT @Locked = 1 FROM ItemGroups WITH (UPDLOCK) WHERE Id = @ItemGroupId;

                SELECT CASE
                    WHEN COUNT(*) = 1
                     AND SUM(CASE WHEN MemberId = @MemberId THEN 1 ELSE 0 END) = 1
                    THEN 1 ELSE 0 END
                FROM Members WHERE ItemGroupId = @ItemGroupId;
                """,
                new { ItemGroupId = itemGroupId, MemberId = memberId },
                transaction: tx,
                cancellationToken: ct
            )
        );
        if (isLastMember == 1)
        {
            return false;
        }

        await db.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM Members WHERE MemberId = @MemberId AND ItemGroupId = @ItemGroupId;

                UPDATE Items SET ReservedFor = NULL, ReservedBy = NULL
                WHERE ItemGroupId = @ItemGroupId AND ReservedFor = @MemberId;
                """,
                new { MemberId = memberId, ItemGroupId = itemGroupId },
                transaction: tx,
                cancellationToken: ct
            )
        );

        tx.Commit();
        return true;
    }
}

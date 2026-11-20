namespace Checklist.Ports;

/// <summary>Who belongs to which list. Every member has the same rights.</summary>
public interface IMembership
{
    Task<bool> IsMember(Guid itemGroupId, Guid userId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> ListMembers(Guid itemGroupId, CancellationToken ct);

    /// <summary>
    /// Adds the member and returns <see langword="true"/>, or returns <see langword="false"/>
    /// when they already are one. Two concurrent adds of the same member must not both succeed.
    /// </summary>
    Task<bool> Add(Guid itemGroupId, Guid memberId, CancellationToken ct);

    /// <summary>
    /// Removes the member and returns <see langword="true"/>, also when they weren't one. The
    /// items reserved for them become free in the same step, so nothing stays reserved for
    /// someone outside the list; the items they reserved for others stay reserved. Returns
    /// <see langword="false"/>, changing nothing, when they're the list's last member: a list
    /// must always keep one. Concurrent removals from one list must not get past this rule
    /// together.
    /// </summary>
    Task<bool> RemoveUnlessLast(Guid itemGroupId, Guid memberId, CancellationToken ct);
}

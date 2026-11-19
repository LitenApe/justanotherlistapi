using Checklist.Domain;

namespace Checklist.Ports;

/// <summary>The lists themselves: their names and settings, loaded with their items.</summary>
public interface IItemGroupStore
{
    /// <summary>Creates the list with <paramref name="firstMemberId"/> as its only member.</summary>
    Task Create(Guid itemGroupId, Name name, Guid firstMemberId, CancellationToken ct);

    /// <summary>
    /// The list with all its items and members, or <see langword="null"/> when it doesn't exist.
    /// </summary>
    Task<ItemGroup?> Load(Guid itemGroupId, CancellationToken ct);

    /// <summary>
    /// The lists <paramref name="memberId"/> belongs to, each with its open items only and without
    /// its members.
    /// </summary>
    Task<IReadOnlyList<ItemGroup>> ListForMember(Guid memberId, CancellationToken ct);

    /// <summary>Renames the list. Changes nothing when it doesn't exist.</summary>
    Task Rename(Guid itemGroupId, Name name, CancellationToken ct);

    /// <summary>
    /// Saves the settings and returns them as stored. <see cref="ItemGroupSettings.AutoDeleteUpdatedAt"/>
    /// moves to <paramref name="now"/> only when the auto-delete delay changes, which restarts its
    /// countdown. Returns <see langword="null"/> when the list doesn't exist.
    /// </summary>
    Task<ItemGroupSettings?> UpdateSettings(
        Guid itemGroupId,
        ItemGroupSettingsInput settings,
        DateTimeOffset now,
        CancellationToken ct
    );

    /// <summary>
    /// Deletes the list with its items and members. Changes nothing when it doesn't exist.
    /// </summary>
    Task Delete(Guid itemGroupId, CancellationToken ct);

    /// <summary>
    /// Deletes every list that is due for auto-delete at <paramref name="now"/>: auto-delete is on,
    /// it has items and none are open, and the delay has passed since the later of the last
    /// completion and the last change to the setting, so turning auto-delete on never deletes a
    /// list straight away. Returns each deleted ID once, even with several app instances.
    /// </summary>
    Task<IReadOnlyList<Guid>> DeleteDueForAutoDelete(DateTimeOffset now, CancellationToken ct);
}

using Checklist.Domain;

namespace Checklist.Ports;

/// <summary>The items in each list.</summary>
public interface IItemStore
{
    Task Create(Item item, CancellationToken ct);

    /// <summary>
    /// Replaces the item's name, description and completion, and returns when it was completed.
    /// Completing sets <paramref name="now"/>; saving a completed item again keeps its original
    /// time, so editing doesn't restart the auto-hide or auto-delete clock; reopening clears it.
    /// Changes nothing, returning <see langword="null"/>, when the item isn't in that list.
    /// </summary>
    Task<DateTimeOffset?> Update(
        Guid itemGroupId,
        Guid itemId,
        ItemInput input,
        DateTimeOffset now,
        CancellationToken ct
    );

    /// <summary>Deletes the item. Changes nothing when the item isn't in that list.</summary>
    Task Delete(Guid itemGroupId, Guid itemId, CancellationToken ct);
}

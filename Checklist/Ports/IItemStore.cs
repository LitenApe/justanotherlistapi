using Checklist.Domain;

namespace Checklist.Ports;

/// <summary>The items in each list.</summary>
public interface IItemStore
{
    Task Create(Item item, CancellationToken ct);

    /// <summary>
    /// Replaces the item's name, description and completion, and returns the item as stored.
    /// Completing sets <paramref name="now"/>; saving a completed item again keeps its original
    /// time, so editing doesn't restart the auto-hide or auto-delete clock; reopening clears it.
    /// Changes nothing, returning <see langword="null"/>, when the item isn't in that list.
    /// </summary>
    Task<Item?> Update(
        Guid itemGroupId,
        Guid itemId,
        ItemInput input,
        DateTimeOffset now,
        CancellationToken ct
    );

    /// <summary>Deletes the item. Changes nothing when the item isn't in that list.</summary>
    Task Delete(Guid itemGroupId, Guid itemId, CancellationToken ct);

    /// <summary>
    /// Applies <paramref name="change"/> if <see cref="ReservationChange.Decide"/> allows it, and
    /// returns the decision, with the item as stored when it changed. Deciding and changing are
    /// one atomic step, which also can't interleave with changes to the list's members or
    /// settings: concurrent changes can't both act on the same reservation, and nothing gets
    /// reserved for a member who is being removed. An item that isn't in that list counts as
    /// <see cref="ReservationDecision.Unchanged"/>.
    /// </summary>
    Task<ReservationResult> ChangeReservation(
        Guid itemGroupId,
        Guid itemId,
        ReservationChange change,
        CancellationToken ct
    );
}

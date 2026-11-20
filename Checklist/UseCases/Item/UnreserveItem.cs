using System.Diagnostics;
using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Cancels an item's reservation, so the item is free for anyone to reserve, as long as the
/// reservation is still the one the caller saw and the list doesn't protect it from the caller.
/// </summary>
public sealed class UnreserveItem(
    IMembership membership,
    IItemStore items,
    IChecklistNotifier notifier
)
{
    public enum Outcome
    {
        /// <summary>Free, also when it already was or the item is gone.</summary>
        Unreserved,
        NotAMember,

        /// <summary>The item is now reserved for someone other than the caller saw.</summary>
        Outdated,

        /// <summary>The list protects the item's reservation from the caller.</summary>
        Protected,
    }

    /// <summary>
    /// Cancels the reservation the caller saw, for <paramref name="memberId"/>, and tells the
    /// list's other members.
    /// </summary>
    public async Task<Outcome> Execute(
        Caller caller,
        Guid itemGroupId,
        Guid itemId,
        Guid memberId,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return Outcome.NotAMember;
        }

        ReservationResult result = await items.ChangeReservation(
            itemGroupId,
            itemId,
            new ReservationChange(caller.UserId, MemberId: null, Replacing: memberId),
            ct
        );
        if (result.Item is { } item)
        {
            await notifier.NotifyItemUpdated(itemGroupId, item, caller.ConnectionId);
        }

        return result.Decision switch
        {
            ReservationDecision.Change or ReservationDecision.Unchanged => Outcome.Unreserved,
            ReservationDecision.Outdated => Outcome.Outdated,
            ReservationDecision.Protected => Outcome.Protected,
            _ => throw new UnreachableException(),
        };
    }
}

using System.Diagnostics;
using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Reserves an item for a member of the list, the caller or someone else, to say who will do
/// it. It takes the item over from whoever had it, as long as the reservation is still the one
/// the caller saw and the list doesn't protect it from the caller.
/// </summary>
public sealed class ReserveItem(
    IMembership membership,
    IItemStore items,
    IChecklistNotifier notifier
)
{
    public enum Outcome
    {
        /// <summary>Reserved, also when it already was or the item is gone.</summary>
        Reserved,
        NotAMember,

        /// <summary>The item's reservation isn't the one the caller saw any more.</summary>
        Outdated,

        /// <summary>The list protects the item's reservation from the caller.</summary>
        Protected,

        /// <summary>The member to reserve it for doesn't belong to the list.</summary>
        ForNonMember,
    }

    /// <summary>
    /// Reserves the item for <paramref name="memberId"/>, replacing the reservation the caller
    /// saw (<paramref name="replacing"/>: who it was reserved for, <see langword="null"/> if it
    /// was free), and tells the list's other members.
    /// </summary>
    public async Task<Outcome> Execute(
        Caller caller,
        Guid itemGroupId,
        Guid itemId,
        Guid memberId,
        Guid? replacing,
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
            new ReservationChange(caller.UserId, memberId, replacing),
            ct
        );
        if (result.Item is { } item)
        {
            await notifier.NotifyItemUpdated(itemGroupId, item, caller.ConnectionId);
        }

        return result.Decision switch
        {
            ReservationDecision.Change or ReservationDecision.Unchanged => Outcome.Reserved,
            ReservationDecision.Outdated => Outcome.Outdated,
            ReservationDecision.Protected => Outcome.Protected,
            ReservationDecision.ForNonMember => Outcome.ForNonMember,
            _ => throw new UnreachableException(),
        };
    }
}

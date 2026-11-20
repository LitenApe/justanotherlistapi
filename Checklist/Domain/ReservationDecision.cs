namespace Checklist.Domain;

/// <summary>What <see cref="ReservationChange.Decide"/> says about a change.</summary>
public enum ReservationDecision
{
    /// <summary>The item is already reserved that way, or already free: there's nothing to do.</summary>
    Unchanged,

    /// <summary>The change may go ahead.</summary>
    Change,

    /// <summary>The reservation isn't the one the caller saw any more.</summary>
    Outdated,

    /// <summary>
    /// The list protects reservations, and the caller is neither the member the item is reserved
    /// for nor whoever reserved it.
    /// </summary>
    Protected,

    /// <summary>The member to reserve the item for doesn't belong to the list.</summary>
    ForNonMember,
}

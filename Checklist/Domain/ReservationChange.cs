namespace Checklist.Domain;

/// <summary>
/// A member's request to change who an item is reserved for. <see cref="MemberId"/> reserves it
/// for that member of the list (the caller or someone else); <see langword="null"/> cancels the
/// reservation. <see cref="Replacing"/> is who the caller saw it reserved for, or
/// <see langword="null"/> if they saw it free, so a change never replaces a reservation the
/// caller hasn't seen.
/// </summary>
/// <param name="CallerId">Who is asking.</param>
/// <param name="MemberId">Who the item should be reserved for, or <see langword="null"/>.</param>
/// <param name="Replacing">Who the caller saw it reserved for, or <see langword="null"/>.</param>
public sealed record ReservationChange(Guid CallerId, Guid? MemberId, Guid? Replacing)
{
    /// <summary>
    /// Whether the change may go ahead, given who the item is reserved for and by now (both
    /// <see langword="null"/> while it's free), whether the list protects reservations, and
    /// whether <see cref="MemberId"/> belongs to the list. The checks run in this order:
    /// <list type="number">
    /// <item>Asking for what's already there changes nothing, so a repeated request is harmless.</item>
    /// <item>Items can only be reserved for members of the list.</item>
    /// <item>The reservation must still be the one the caller saw.</item>
    /// <item>
    /// In a list that protects reservations, only the member an item is reserved for and whoever
    /// reserved it may change or cancel the reservation. Any member may reserve a free item.
    /// </item>
    /// </list>
    /// </summary>
    public ReservationDecision Decide(
        Guid? reservedFor,
        Guid? reservedBy,
        bool protectReservations,
        bool memberBelongs
    )
    {
        if (reservedFor == MemberId && (MemberId is null || reservedBy == CallerId))
        {
            return ReservationDecision.Unchanged;
        }
        if (MemberId is not null && !memberBelongs)
        {
            return ReservationDecision.ForNonMember;
        }
        if (reservedFor != Replacing)
        {
            return ReservationDecision.Outdated;
        }
        if (
            protectReservations
            && reservedFor is not null
            && CallerId != reservedFor
            && CallerId != reservedBy
        )
        {
            return ReservationDecision.Protected;
        }
        return ReservationDecision.Change;
    }
}

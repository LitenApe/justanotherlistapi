namespace Checklist.Domain;

/// <summary>
/// What became of a <see cref="ReservationChange"/>: the decision, and the item as stored when
/// the decision was <see cref="ReservationDecision.Change"/>.
/// </summary>
/// <param name="Decision">What <see cref="ReservationChange.Decide"/> said.</param>
/// <param name="Item">The changed item, or <see langword="null"/> when nothing changed.</param>
public sealed record ReservationResult(ReservationDecision Decision, Item? Item);

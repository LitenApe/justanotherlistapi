using System.ComponentModel;

namespace Core.Checklist;

public sealed record ItemGroupSettings
{
    /// <summary>Shortest auto-delete delay (1 day), so a mistaken last check can still be undone.</summary>
    public const int MinAutoDeleteMinutes = 24 * 60;

    /// <summary>Longest delay for either setting (365 days).</summary>
    public const int MaxMinutes = 365 * 24 * 60;

    [Description(
        "Minutes after the last item was completed before the list is deleted. Null when auto-delete is off"
    )]
    public int? AutoDeleteAfterMinutes { get; init; }

    [Description(
        "Minutes after an item was completed before clients hide it. Null when auto-hide is off"
    )]
    public int? HideCompletedAfterMinutes { get; init; }

    [Description(
        "When AutoDeleteAfterMinutes last changed. The auto-delete countdown never starts before this time"
    )]
    public DateTimeOffset? AutoDeleteUpdatedAt { get; init; }
}

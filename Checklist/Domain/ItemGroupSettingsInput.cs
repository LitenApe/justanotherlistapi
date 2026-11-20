using System.Diagnostics.CodeAnalysis;

namespace Checklist.Domain;

/// <summary>
/// The settings a member chooses for a list: each delay off (<see langword="null"/>) or within
/// its range, and whether reservations are protected. When the auto-delete countdown starts is
/// up to the server.
/// </summary>
public sealed record ItemGroupSettingsInput
{
    private ItemGroupSettingsInput(
        int? autoDeleteAfterMinutes,
        int? hideCompletedAfterMinutes,
        bool protectReservations
    )
    {
        AutoDeleteAfterMinutes = autoDeleteAfterMinutes;
        HideCompletedAfterMinutes = hideCompletedAfterMinutes;
        ProtectReservations = protectReservations;
    }

    public int? AutoDeleteAfterMinutes { get; }

    public int? HideCompletedAfterMinutes { get; }

    public bool ProtectReservations { get; }

    /// <summary>
    /// Accepts an auto-delete delay from <see cref="ItemGroupSettings.MinAutoDeleteMinutes"/> and
    /// an auto-hide delay from 0, both up to <see cref="ItemGroupSettings.MaxMinutes"/>.
    /// </summary>
    public static bool TryCreate(
        int? autoDeleteAfterMinutes,
        int? hideCompletedAfterMinutes,
        bool protectReservations,
        [NotNullWhen(true)] out ItemGroupSettingsInput? settings
    )
    {
        settings =
            IsInRange(autoDeleteAfterMinutes, ItemGroupSettings.MinAutoDeleteMinutes)
            && IsInRange(hideCompletedAfterMinutes, 0)
                ? new ItemGroupSettingsInput(
                    autoDeleteAfterMinutes,
                    hideCompletedAfterMinutes,
                    protectReservations
                )
                : null;
        return settings is not null;
    }

    private static bool IsInRange(int? minutes, int min) =>
        minutes is null || (minutes >= min && minutes <= ItemGroupSettings.MaxMinutes);
}

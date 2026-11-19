using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Replaces a list's auto-delete and auto-hide settings.</summary>
public sealed class UpdateItemGroupSettings(
    IMembership membership,
    IItemGroupStore itemGroups,
    IChecklistNotifier notifier,
    TimeProvider timeProvider
)
{
    /// <summary>Saves the settings and tells the list's other members.</summary>
    /// <returns>
    /// The settings as stored, or <see langword="null"/> when the caller isn't a member.
    /// </returns>
    public async Task<ItemGroupSettings?> Execute(
        Caller caller,
        Guid itemGroupId,
        ItemGroupSettingsInput input,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return null;
        }

        // The store only finds no list if it was deleted after the membership check.
        ItemGroupSettings settings = await itemGroups.UpdateSettings(
            itemGroupId,
            input,
            timeProvider.GetUtcNow(),
            ct
        )
            ?? new ItemGroupSettings
            {
                AutoDeleteAfterMinutes = input.AutoDeleteAfterMinutes,
                HideCompletedAfterMinutes = input.HideCompletedAfterMinutes,
            };
        await notifier.NotifyGroupSettingsChanged(itemGroupId, settings, caller.ConnectionId);
        return settings;
    }
}

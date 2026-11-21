using Checklist.Domain;

namespace Checklist.Ports;

/// <summary>
/// Tells a list's members about its changes as they happen. Use cases pass
/// <see cref="Caller.ConnectionId"/> on as <c>excludeConnectionId</c> without reading it, so
/// whoever made the change doesn't get it back; <see langword="null"/> tells every member.
/// </summary>
public interface IChecklistNotifier
{
    Task NotifyItemCreated(Guid groupId, Item item, string? excludeConnectionId);
    Task NotifyItemUpdated(Guid groupId, Item item, string? excludeConnectionId);
    Task NotifyItemDeleted(Guid groupId, Guid itemId, string? excludeConnectionId);
    Task NotifyMemberAdded(Guid groupId, Member member, string? excludeConnectionId);
    Task NotifyMemberRemoved(Guid groupId, Guid memberId, string? excludeConnectionId);
    Task NotifyGroupRenamed(Guid groupId, string name, string? excludeConnectionId);
    Task NotifyGroupSettingsChanged(
        Guid groupId,
        ItemGroupSettings settings,
        string? excludeConnectionId
    );
    Task NotifyGroupDeleted(Guid groupId, string? excludeConnectionId);
}

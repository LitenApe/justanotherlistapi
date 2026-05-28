namespace Core.Checklist;

public interface IChecklistNotifier
{
    Task NotifyItemCreated(Guid groupId, Item item, string? excludeConnectionId);
    Task NotifyItemUpdated(Guid groupId, Item item, string? excludeConnectionId);
    Task NotifyItemDeleted(Guid groupId, Guid itemId, string? excludeConnectionId);
    Task NotifyMemberAdded(Guid groupId, Guid memberId, string? excludeConnectionId);
    Task NotifyMemberRemoved(Guid groupId, Guid memberId, string? excludeConnectionId);
    Task NotifyGroupRenamed(Guid groupId, string name, string? excludeConnectionId);
    Task NotifyGroupDeleted(Guid groupId, string? excludeConnectionId);
}

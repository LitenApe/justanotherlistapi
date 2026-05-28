namespace Core.Checklist;

public interface IChecklistClient
{
    Task ItemCreated(Guid groupId, Item item);
    Task ItemUpdated(Guid groupId, Item item);
    Task ItemDeleted(Guid groupId, Guid itemId);
    Task MemberAdded(Guid groupId, Guid memberId);
    Task MemberRemoved(Guid groupId, Guid memberId);
    Task GroupRenamed(Guid groupId, string name);
    Task GroupDeleted(Guid groupId);
}

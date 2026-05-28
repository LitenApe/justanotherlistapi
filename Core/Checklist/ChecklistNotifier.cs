using Microsoft.AspNetCore.SignalR;

namespace Core.Checklist;

public sealed class ChecklistNotifier(IHubContext<ChecklistHub, IChecklistClient> hubContext)
    : IChecklistNotifier
{
    public Task NotifyItemCreated(Guid groupId, Item item, string? excludeConnectionId)
    {
        return GetClients(groupId, excludeConnectionId).ItemCreated(groupId, item);
    }

    public Task NotifyItemUpdated(Guid groupId, Item item, string? excludeConnectionId)
    {
        return GetClients(groupId, excludeConnectionId).ItemUpdated(groupId, item);
    }

    public Task NotifyItemDeleted(Guid groupId, Guid itemId, string? excludeConnectionId)
    {
        return GetClients(groupId, excludeConnectionId).ItemDeleted(groupId, itemId);
    }

    public Task NotifyMemberAdded(Guid groupId, Guid memberId, string? excludeConnectionId)
    {
        return GetClients(groupId, excludeConnectionId).MemberAdded(groupId, memberId);
    }

    public Task NotifyMemberRemoved(Guid groupId, Guid memberId, string? excludeConnectionId)
    {
        return GetClients(groupId, excludeConnectionId).MemberRemoved(groupId, memberId);
    }

    public Task NotifyGroupRenamed(Guid groupId, string name, string? excludeConnectionId)
    {
        return GetClients(groupId, excludeConnectionId).GroupRenamed(groupId, name);
    }

    public Task NotifyGroupDeleted(Guid groupId, string? excludeConnectionId)
    {
        return GetClients(groupId, excludeConnectionId).GroupDeleted(groupId);
    }

    private IChecklistClient GetClients(Guid groupId, string? excludeConnectionId)
    {
        string group = groupId.ToString();
        if (excludeConnectionId is not null)
        {
            return hubContext.Clients.GroupExcept(group, [excludeConnectionId]);
        }
        return hubContext.Clients.Group(group);
    }
}

using Core.Checklist;

namespace Core.Tests;

internal sealed class CapturingNotifier : IChecklistNotifier
{
    public List<object> Notifications { get; } = [];

    public Task NotifyItemCreated(Guid groupId, Item item, string? excludeConnectionId)
    {
        Notifications.Add(new ItemCreatedNotification(groupId, item, excludeConnectionId));
        return Task.CompletedTask;
    }

    public Task NotifyItemUpdated(Guid groupId, Item item, string? excludeConnectionId)
    {
        Notifications.Add(new ItemUpdatedNotification(groupId, item, excludeConnectionId));
        return Task.CompletedTask;
    }

    public Task NotifyItemDeleted(Guid groupId, Guid itemId, string? excludeConnectionId)
    {
        Notifications.Add(new ItemDeletedNotification(groupId, itemId, excludeConnectionId));
        return Task.CompletedTask;
    }

    public Task NotifyMemberAdded(Guid groupId, Guid memberId, string? excludeConnectionId)
    {
        Notifications.Add(new MemberAddedNotification(groupId, memberId, excludeConnectionId));
        return Task.CompletedTask;
    }

    public Task NotifyMemberRemoved(Guid groupId, Guid memberId, string? excludeConnectionId)
    {
        Notifications.Add(new MemberRemovedNotification(groupId, memberId, excludeConnectionId));
        return Task.CompletedTask;
    }

    public Task NotifyGroupRenamed(Guid groupId, string name, string? excludeConnectionId)
    {
        Notifications.Add(new GroupRenamedNotification(groupId, name, excludeConnectionId));
        return Task.CompletedTask;
    }

    public Task NotifyGroupDeleted(Guid groupId, string? excludeConnectionId)
    {
        Notifications.Add(new GroupDeletedNotification(groupId, excludeConnectionId));
        return Task.CompletedTask;
    }

    internal sealed record ItemCreatedNotification(
        Guid GroupId,
        Item Item,
        string? ExcludeConnectionId
    );

    internal sealed record ItemUpdatedNotification(
        Guid GroupId,
        Item Item,
        string? ExcludeConnectionId
    );

    internal sealed record ItemDeletedNotification(
        Guid GroupId,
        Guid ItemId,
        string? ExcludeConnectionId
    );

    internal sealed record MemberAddedNotification(
        Guid GroupId,
        Guid MemberId,
        string? ExcludeConnectionId
    );

    internal sealed record MemberRemovedNotification(
        Guid GroupId,
        Guid MemberId,
        string? ExcludeConnectionId
    );

    internal sealed record GroupRenamedNotification(
        Guid GroupId,
        string Name,
        string? ExcludeConnectionId
    );

    internal sealed record GroupDeletedNotification(Guid GroupId, string? ExcludeConnectionId);
}

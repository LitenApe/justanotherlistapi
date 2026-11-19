using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>
/// Decides whether a caller may receive a list's realtime updates: only its members may. The
/// realtime adapter does the subscribing itself.
/// </summary>
public sealed class SubscribeToItemGroup(IMembership membership)
{
    public Task<bool> Execute(Caller caller, Guid itemGroupId, CancellationToken ct) =>
        membership.IsMember(itemGroupId, caller.UserId, ct);
}

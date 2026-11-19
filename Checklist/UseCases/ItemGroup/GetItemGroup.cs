using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>One list with all its items and members.</summary>
public sealed class GetItemGroup(IMembership membership, IItemGroupStore itemGroups)
{
    /// <summary>Loads the list for one of its members.</summary>
    /// <returns>
    /// The list, or <see langword="null"/> when the caller isn't a member (or the list was deleted
    /// a moment ago).
    /// </returns>
    public async Task<ItemGroup?> Execute(Caller caller, Guid itemGroupId, CancellationToken ct)
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return null;
        }

        return await itemGroups.Load(itemGroupId, ct);
    }
}

using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Creates a list. Anyone signed in may, and becomes its first member.</summary>
public sealed class CreateItemGroup(IItemGroupStore itemGroups, IMembership membership)
{
    public async Task<ItemGroup> Execute(Caller caller, Name name, CancellationToken ct)
    {
        var itemGroupId = Guid.NewGuid();
        await itemGroups.Create(itemGroupId, name, caller.UserId, ct);
        return new ItemGroup
        {
            Id = itemGroupId,
            Name = name.Value,
            // The creator, with their name.
            Members = await membership.ListMembers(itemGroupId, ct),
        };
    }
}

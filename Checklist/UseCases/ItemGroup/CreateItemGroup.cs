using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Creates a list. Anyone signed in may, and becomes its first member.</summary>
public sealed class CreateItemGroup(IItemGroupStore itemGroups)
{
    public async Task<ItemGroup> Execute(Caller caller, Name name, CancellationToken ct)
    {
        var itemGroup = new ItemGroup
        {
            Id = Guid.NewGuid(),
            Name = name.Value,
            Members = [caller.UserId],
        };
        await itemGroups.Create(itemGroup.Id, name, caller.UserId, ct);
        return itemGroup;
    }
}

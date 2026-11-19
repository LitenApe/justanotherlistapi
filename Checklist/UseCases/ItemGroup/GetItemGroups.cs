using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>The caller's lists, each with its open items.</summary>
public sealed class GetItemGroups(IItemGroupStore itemGroups)
{
    public Task<IReadOnlyList<ItemGroup>> Execute(Caller caller, CancellationToken ct) =>
        itemGroups.ListForMember(caller.UserId, ct);
}

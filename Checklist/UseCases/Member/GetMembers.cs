using Checklist.Domain;
using Checklist.Ports;

namespace Checklist.UseCases;

/// <summary>Lists a list's members. Only members may see them.</summary>
public sealed class GetMembers(IMembership membership)
{
    /// <summary>Returns the members, with their names.</summary>
    /// <returns>The members, or <see langword="null"/> when the caller isn't a member.</returns>
    public async Task<IReadOnlyList<Member>?> Execute(
        Caller caller,
        Guid itemGroupId,
        CancellationToken ct
    )
    {
        if (!await membership.IsMember(itemGroupId, caller.UserId, ct))
        {
            return null;
        }

        return await membership.ListMembers(itemGroupId, ct);
    }
}

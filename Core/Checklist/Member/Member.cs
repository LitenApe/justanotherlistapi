using System.ComponentModel;

namespace Core.Checklist;

public record Member
{
    [Description("Unique identifier of the member")]
    public required Guid MemberId { get; init; }

    [Description("Identifier of the item group this member belongs to")]
    public required Guid ItemGroupId { get; init; }
}

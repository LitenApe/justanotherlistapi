using System.ComponentModel;

namespace Core.Checklist;

public record Item
{
    [Description("Unique identifier of the item")]
    public Guid Id { get; init; }

    [Description("Name of the item")]
    public required string Name { get; init; }

    [Description("Description of the item")]
    public string? Description { get; init; }

    [Description("Indicates whether the item is complete")]
    public bool IsComplete { get; init; }

    [Description("Identifier of the item group this item belongs to")]
    public required Guid ItemGroupId { get; init; }
}

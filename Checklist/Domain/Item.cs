using System.ComponentModel;

namespace Checklist.Domain;

public sealed record Item
{
    [Description("Unique identifier of the item")]
    public Guid Id { get; init; }

    [Description("Name of the item")]
    public required string Name { get; init; }

    [Description("Description of the item")]
    public string? Description { get; init; }

    [Description("Indicates whether the item is complete")]
    public bool IsComplete { get; init; }

    [Description("When the item was completed. Set by the server; null while the item is open")]
    public DateTimeOffset? CompletedAt { get; init; }

    [Description("Identifier of the item group this item belongs to")]
    public required Guid ItemGroupId { get; init; }

    [Description("The member who will do the item, or null while nobody has reserved it")]
    public Guid? ReservedFor { get; init; }

    [Description(
        "The member who reserved the item, the same as reservedFor when members reserve items for themselves. Null while nobody has reserved it"
    )]
    public Guid? ReservedBy { get; init; }
}

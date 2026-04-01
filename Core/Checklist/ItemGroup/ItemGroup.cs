using System.ComponentModel;

namespace Core.Checklist;

public record ItemGroup
{
    [Description("Unique identifier of the item group")]
    public Guid Id { get; init; }

    [Description("Name of the item group")]
    public required string Name { get; init; }

    [Description("Description of the item group")]
    public IReadOnlyList<Item> Items { get; init; } = [];

    [Description("Members of the item group")]
    public IReadOnlyList<Guid> Members { get; init; } = [];
}

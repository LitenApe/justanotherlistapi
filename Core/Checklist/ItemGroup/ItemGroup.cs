using System.ComponentModel;

namespace Core.Checklist;

public record ItemGroup
{
    [Description("Unique identifier of the item group")]
    public Guid Id { get; init; }

    [Description("Name of the item group")]
    public required string Name { get; init; }

    [Description("Description of the item group")]
    public List<Item> Items { get; init; } = [];

    [Description("Members of the item group")]
    public List<Guid> Members { get; init; } = [];
}

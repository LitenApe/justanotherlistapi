using System.ComponentModel;

namespace Core.Checklist;

public sealed record ItemGroup
{
    [Description("Unique identifier of the item group")]
    public Guid Id { get; init; }

    [Description("Name of the item group")]
    public required string Name { get; init; }

    [Description("Description of the item group")]
    public IReadOnlyList<Item> Items { get; init; } = [];

    [Description("Members of the item group")]
    public IReadOnlyList<Guid> Members { get; init; } = [];

    [Description("Auto-delete and auto-hide settings, shared by all members")]
    public ItemGroupSettings Settings { get; init; } = new();
}

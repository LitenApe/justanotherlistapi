using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Checklist;

public class ItemGroup
{
    [Description("Unique identifier of the item group")]
    public Guid Id { get; set; }

    [Description("Name of the item group")]
    public required string Name { get; set; }

    [Description("Description of the item group")]
    public ICollection<Item> Items { get; } = [];

    [Description("Members of the item group")]
    public ICollection<Member> Members { get; } = [];
}

public class ItemGroupConfiguration : IEntityTypeConfiguration<ItemGroup>
{
    public void Configure(EntityTypeBuilder<ItemGroup> builder)
    {
        builder.HasKey(ig => ig.Id);
        builder.HasIndex(ig => ig.Id);
        builder.Property(ig => ig.Id).ValueGeneratedOnAdd();
    }
}

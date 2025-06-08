using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Checklist;

public class Item
{
    [Description("Unique identifier of the item")]
    public Guid Id { get; set; }

    [Description("Name of the item")]
    public required string Name { get; set; }

    [Description("Description of the item")]
    public string? Description { get; set; }

    [Description("Indicates whether the item is complete")]
    public bool IsComplete { get; set; }

    [Description("Identifier of the item group this item belongs to")]
    public required Guid ItemGroupId { get; set; }
}

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => i.Id);
        builder.HasIndex(i => new { i.Id, i.ItemGroupId });
        builder.Property(i => i.Id).ValueGeneratedOnAdd();
        builder.HasOne<ItemGroup>().WithMany(ig => ig.Items).HasForeignKey(i => i.ItemGroupId);
    }
}
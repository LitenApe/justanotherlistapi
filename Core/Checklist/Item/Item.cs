using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Checklist;

public class Item
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsComplete { get; set; }
    public required Guid ItemGroupId { get; set; }
}

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.HasOne<ItemGroup>().WithMany(ig => ig.Items).HasForeignKey(i => i.ItemGroupId);
    }
}
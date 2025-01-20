using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JustAnotherListAPI.Checklist.Model
{
    public class Item
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public bool IsComplete { get; set; }
        public required Guid ItemGroupId { get; set; }

        public static Item Create(Guid itemGroupId, ItemDTO item)
        {
            return new Item()
            {
                Name = item.Name,
                Description = item.Description,
                IsComplete = item.IsComplete,
                ItemGroupId = itemGroupId
            };
        }

        public void Update(ItemDTO item)
        {
            Name = item.Name;
            Description = item.Description;
            IsComplete = item.IsComplete;
        }
    }
    public class ItemConfiguration : IEntityTypeConfiguration<Item>
    {
        public void Configure(EntityTypeBuilder<Item> builder)
        {
            builder.HasOne<ItemGroup>().WithMany(ig => ig.Items).HasForeignKey(i => i.ItemGroupId);
        }
    }
}
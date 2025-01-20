using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JustAnotherListAPI.Checklist.Model
{
  public class ItemGroup
  {
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Item> Items { get; } = [];
    public ICollection<Member> Members { get; } = [];

    public static ItemGroup Create(ItemGroupDTO itemGroup)
    {
      return new ()
      {
        Name = itemGroup.Name
      };
    }

    public void Update(ItemGroupDTO itemGroup)
    {
      Name = itemGroup.Name;
    }
  }

  public class ItemGroupConfiguration : IEntityTypeConfiguration<ItemGroup>
  {
      public void Configure(EntityTypeBuilder<ItemGroup> builder)
      {
        builder.Property(ig => ig.Id).HasDefaultValueSql();
      }
  }
}
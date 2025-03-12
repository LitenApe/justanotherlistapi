using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JustAnotherListApi.Checklist;

public class ItemGroup
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Item> Items { get; } = [];
    public ICollection<Member> Members { get; } = [];
}

public class ItemGroupConfiguration : IEntityTypeConfiguration<ItemGroup>
{
    public void Configure(EntityTypeBuilder<ItemGroup> builder)
    {
        builder.Property(ig => ig.Id).HasDefaultValueSql();
    }
}

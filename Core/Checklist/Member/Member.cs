using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Core.Checklist;

public class Member
{
    [Description("Unique identifier of the member")]
    public required Guid MemberId { get; set; }

    [Description("Identifier of the item group this member belongs to")]
    public required Guid ItemGroupId { get; set; }
}

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(nameof(Member.MemberId), nameof(Member.ItemGroupId));
        builder.HasIndex(m => new { m.MemberId, m.ItemGroupId });
        builder.HasOne<ItemGroup>().WithMany(ig => ig.Members).HasForeignKey(m => m.ItemGroupId);
    }
}

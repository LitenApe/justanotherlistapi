using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JustAnotherListApi.Checklist;

public class Member
{
    public required Guid MemberId { get; set; }
    public required Guid ItemGroupId { get; set; }
}

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasOne<ItemGroup>().WithMany(ig => ig.Members).HasForeignKey(m => m.ItemGroupId);
        builder.HasKey(nameof(Member.MemberId), nameof(Member.ItemGroupId));
    }
}

using Microsoft.EntityFrameworkCore;
using Core.Checklist;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Core;
public class DatabaseContext : IdentityDbContext<IdentityUser>
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options)
      : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemGroup> ItemGroups => Set<ItemGroup>();
    public DbSet<Member> Members => Set<Member>();

    public Task<bool> IsMember(Guid itemGroup, Guid? user, CancellationToken ct = default)
    {
        if (user is null)
        {
            return Task.FromResult(false);
        }

        return Members.AnyAsync(m => m.ItemGroupId == itemGroup && m.MemberId == user, ct);
    }
}
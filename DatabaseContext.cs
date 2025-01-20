using Microsoft.EntityFrameworkCore;
using JustAnotherListAPI.Checklist.Model;
class DatabaseContext : DbContext
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
}
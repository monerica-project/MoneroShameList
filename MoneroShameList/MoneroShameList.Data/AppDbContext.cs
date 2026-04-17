using Microsoft.EntityFrameworkCore;
using MoneroShameList.Models;

namespace MoneroShameList.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ShameEntry> ShameEntries => Set<ShameEntry>();
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShameEntry>()
            .HasIndex(e => e.Slug)
            .IsUnique();
    }
}
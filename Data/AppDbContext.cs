using Microsoft.EntityFrameworkCore;
using MyApp.Models;

namespace MyApp.Data
{
  public class AppDbContext : DbContext
  {
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Entry> Entries => Set<Entry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      // Relationships
      modelBuilder.Entity<Entry>()
          .HasOne(e => e.Player)
          .WithMany(p => p.Entries)
          .HasForeignKey(e => e.PlayerId);

      modelBuilder.Entity<Entry>()
          .HasOne(e => e.Round)
          .WithMany(r => r.Entries)
          .HasForeignKey(e => e.RoundId);
    }
  }
}

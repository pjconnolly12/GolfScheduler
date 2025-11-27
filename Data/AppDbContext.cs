using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyApp.Models;

namespace MyApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Player> Players => Set<Player>();
        public DbSet<Round> Rounds => Set<Round>();
        public DbSet<Entry> Entries => Set<Entry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //
            // 🔗 Link ApplicationUser ↔ Player
            //
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Player)
                .WithMany()
                .HasForeignKey(u => u.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            //
            // 🔽 Your Existing Configuration
            //

            // Seed test players
            modelBuilder.Entity<Player>().HasData(
                new Player { Id = 1, Name = "Patrick Connolly", Email = "patrick@example.com" },
                new Player { Id = 2, Name = "Jordan Smith", Email = "jordan@example.com" }
            );

            // Seed test rounds
            modelBuilder.Entity<Round>().HasData(
                new Round { Id = 1, Date = DateTime.Today.AddDays(3), Course = "Pebble Beach", Notes = "Morning tee time" },
                new Round { Id = 2, Date = DateTime.Today.AddDays(10), Course = "Augusta National", Notes = "Afternoon round" }
            );

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

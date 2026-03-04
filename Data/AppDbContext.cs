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
        public DbSet<DistributionListMember> DistributionListMembers => Set<DistributionListMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //
            // 🔗 Link ApplicationUser ↔ Player
            //
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Player)
                .WithOne()
                .HasForeignKey<Player>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<DistributionListMember>()
                .HasKey(m => new { m.OwnerUserId, m.MemberUserId });

            modelBuilder.Entity<DistributionListMember>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(m => m.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DistributionListMember>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(m => m.MemberUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

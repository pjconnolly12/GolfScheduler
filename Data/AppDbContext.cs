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

            // Store user-facing schedule values as wall-clock timestamps.
            modelBuilder.Entity<Round>()
                .Property(r => r.Date)
                .HasColumnType("timestamp without time zone");

            modelBuilder.Entity<Entry>()
                .Property(e => e.ExpiresAt)
                .HasColumnType("timestamp without time zone");

            // Store system/audit values in UTC-aware columns.
            modelBuilder.Entity<Entry>()
                .Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<Round>()
                .Property(r => r.ReminderSentAtUtc)
                .HasColumnType("timestamp with time zone");

            modelBuilder.Entity<Entry>()
                .Property(e => e.MaybeReminderSentAtUtc)
                .HasColumnType("timestamp with time zone");

            //
            // 🔗 Link ApplicationUser ↔ Player
            //
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Player)
                .WithOne()
                .HasForeignKey<Player>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Player>().ToTable("Players");
            modelBuilder.Entity<Round>().ToTable("Rounds");
            modelBuilder.Entity<Entry>().ToTable("Entries");
            modelBuilder.Entity<DistributionListMember>().ToTable("DistributionListMembers");

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

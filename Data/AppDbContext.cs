using Microsoft.EntityFrameworkCore;
using MyApp.Models;

namespace MyApp.Data
{
  public class AppDbContext : DbContext
  {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players { get; set; }
  }
}

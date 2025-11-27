using Microsoft.AspNetCore.Identity;
using MyApp.Data;
using MyApp.Models;

public class PlayerService
{
  private readonly ApplicationDbContext _context;
  private readonly UserManager<ApplicationUser> _userManager;

  public PlayerService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
  {
    _context = context;
    _userManager = userManager;
  }

  public async Task EnsurePlayerForUser(ApplicationUser user)
  {
    if (user.PlayerId != null) return; // Already has a Player

    var player = new Player
    {
      Name = user.UserName ?? "Unknown",
      Email = user.Email
    };

    _context.Players.Add(player);
    await _context.SaveChangesAsync();

    user.PlayerId = player.Id;
    await _userManager.UpdateAsync(user);
  }
}

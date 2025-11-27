using Microsoft.AspNetCore.Identity;

namespace MyApp.Models
{
  public class ApplicationUser : IdentityUser
  {
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }
  }
}
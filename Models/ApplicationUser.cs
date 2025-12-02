using Microsoft.AspNetCore.Identity;

namespace MyApp.Models
{
  public class ApplicationUser : IdentityUser
  {
    public string? PlayerId { get; set; }      // optional
    public virtual Player? Player { get; set; }
  }
}
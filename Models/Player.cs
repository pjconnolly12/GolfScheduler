using System.ComponentModel.DataAnnotations;

namespace MyApp.Models
{
  public class Player
  {
    public string Id { get; set; } = Guid.NewGuid().ToString(); // PK
    public string UserId { get; set; }  // link to ApplicationUser.Id
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    public virtual ICollection<Entry> Entries { get; set; } = new List<Entry>();
  }
}
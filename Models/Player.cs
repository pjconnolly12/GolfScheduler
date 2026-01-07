using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MyApp.Models
{
  public class Player
  {
    public string Id { get; set; } = Guid.NewGuid().ToString(); // PK
    public string UserId { get; set; }  // link to ApplicationUser.Id
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    public string DistributionEmailsJson { get; set; } = "[]";

    // Not mapped directly — EF uses JSON column above
    [NotMapped]
    public List<string> DistributionEmails
    {
      get => string.IsNullOrWhiteSpace(DistributionEmailsJson)
              ? new List<string>()
              : JsonSerializer.Deserialize<List<string>>(DistributionEmailsJson)!;

      set => DistributionEmailsJson = JsonSerializer.Serialize(value ?? new List<string>());
    }

    public virtual ICollection<Entry> Entries { get; set; } = new List<Entry>();
  }
}
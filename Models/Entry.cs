namespace MyApp.Models
{
  public class Entry
  {
    public int Id { get; set; }

    // Foreign keys
    public int RoundId { get; set; }
    public string? PlayerId { get; set; }

    // Navigation properties
    public Round Round { get; set; } = default!;
    public Player Player { get; set; } = default!;

    // Optional — additional fields specific to the signup
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = "";
    public int? Guests { get; set; }
  }
}
namespace MyApp.Models
{
  public class Player
  {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }

    // Navigation property — one player can have many entries
    public List<Entry> Entries { get; set; } = new();
  }
}
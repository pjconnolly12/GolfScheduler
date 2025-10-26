namespace MyApp.Models
{
  public class Round
  {
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Course { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Navigation property — a round can have many entries
    public List<Entry> Entries { get; set; } = new();
  }
}
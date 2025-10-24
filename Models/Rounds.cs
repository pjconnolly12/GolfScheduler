namespace MyApp.Models
{
  public class Rounds
  {
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public Player[] Players { get; set; } = [];
  }
}
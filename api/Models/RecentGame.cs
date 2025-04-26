namespace ArenaBackend.Models
{
  public class RecentGame
  {
    public string MatchId { get; set; } = string.Empty;
    public List<object> Players { get; set; } = new();
    public DateTime GameCreation { get; set; }
    public long GameDuration { get; set; }
  }
}

namespace ArenaBackend.Models;

public class PlayerDTO
{
   public string GameName { get; set; } = string.Empty;
   public string TagLine { get; set; } = string.Empty;
   public int ChampionId { get; set; }
   public string ChampionName { get; set; } = string.Empty;
   public int Placement { get; set; }
   public List<string> Augments { get; set; } = new List<string>();
   public List<int> Items { get; set; } = new List<int>();
   public int Kills { get; set; }
   public int Deaths { get; set; }
   public int Assists { get; set; }
   public int TotalDamageDealt { get; set; }
}

public class DetailedMatch
{
   public string MatchId { get; set; } = string.Empty;
   public List<PlayerDTO> Players { get; set; } = new List<PlayerDTO>();
   public DateTime GameCreation { get; set; }
}
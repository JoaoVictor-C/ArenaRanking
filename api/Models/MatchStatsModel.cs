using MongoDB.Bson.Serialization.Attributes;

namespace ArenaBackend.Models;
public class MatchStats
{
   [BsonElement("win")]
   public int Win { get; set; } = 0;
   
   [BsonElement("loss")]
   public int Loss { get; set; } = 0;
   
   [BsonElement("lastProcessedMatchId")]
   public string LastProcessedMatchId { get; set; } = string.Empty;
   
   [BsonElement("recentGames")]
   public List<DetailedMatch> RecentGames { get; set; } = new List<DetailedMatch>();

   [BsonElement("averagePlacement")]
   public double AveragePlacement { get; set; } = 0;

   [BsonElement("championsPlayed")]
   public List<ChampionPlayed> ChampionsPlayed { get; set; } = new List<ChampionPlayed>();

      
   [BsonIgnore]
   public int TotalGames => Win + Loss;
   
   [BsonIgnore]
   public double WinRate => TotalGames > 0 ? (double)Win / TotalGames : 0;
}

public class ChampionPlayed
{
    public string ChampionId { get; set; } = string.Empty;
    public string ChampionName { get; set; } = string.Empty;
}
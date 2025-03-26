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
   public List<string> RecentGames { get; set; } = new List<string>();

   [BsonElement("averagePlacement")]
   public double AveragePlacement { get; set; } = 0;

   [BsonElement("championsPlayed")]
   public List<Dictionary<string, int>> ChampionsPlayed { get; set; } = new List<Dictionary<string, int>>();
      
   [BsonIgnore]
   public int TotalGames => Win + Loss;
   
   [BsonIgnore]
   public double WinRate => TotalGames > 0 ? (double)Win / TotalGames : 0;
}

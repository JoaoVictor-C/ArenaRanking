using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ArenaBackend.Models;

public class Player
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    // Riot account identifiers
    public string Puuid { get; set; } = string.Empty;
    public string RiotId { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    
    // Ranking information
    public int MmrAtual { get; set; } = 1000;
    public int DeltaMmr { get; set; } = 0;
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
    
    // Match processing data
    public string UltimoMatchIdProcessado { get; set; } = string.Empty;
    public bool AutoCheck { get; set; } = false;
    
    // Metadata
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DateAdded { get; set; }
    
    // Computed properties
    [BsonIgnore]
    public int TotalGames => Wins + Losses;
    
    [BsonIgnore]
    public double WinRate => TotalGames > 0 ? (double)Wins / TotalGames : 0;
}
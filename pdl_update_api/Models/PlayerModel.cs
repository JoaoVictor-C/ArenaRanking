using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace ArenaBackend.Models;

public class Player
{
   [BsonId]
   [BsonRepresentation(BsonType.ObjectId)]
   public string Id { get; set; } = null!;
   
   [BsonElement("puuid")]
   public string Puuid { get; set; } = string.Empty;
   
   [BsonElement("tagLine")]
   public string TagLine { get; set; } = string.Empty;
   
   [BsonElement("gameName")]
   public string GameName { get; set; } = string.Empty;
   
   [BsonElement("profileIconId")]
   public int ProfileIconId { get; set; } = 0;

   [BsonElement("region")]
   public string Region { get; set; } = "americas";

   [BsonElement("server")]
    public string Server { get; set; } = "br1";
   
   [BsonElement("pdl")]
   public int Pdl { get; set; } = 1000;
   
   [BsonElement("rankPosition")]
   public int RankPosition { get; set; } = 0;
   
   [BsonElement("matchStats")]
   public MatchStats MatchStats { get; set; } = new MatchStats();

   [BsonElement("lastPlacement")]
    public int LastPlacement { get; set; } = 0;

   
   private DateTime? _lastUpdate;
   [BsonElement("lastUpdate")]
   [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
   public DateTime? LastUpdate 
   { 
       get => _lastUpdate?.ToLocalTime(); 
       set => _lastUpdate = value; 
   }
   
   [BsonElement("trackingEnabled")]
   public bool TrackingEnabled { get; set; } = false;
   
   private DateTime? _dateAdded = DateTime.UtcNow;
   [BsonElement("dateAdded")]
   [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
   public DateTime? DateAdded 
   { 
       get => _dateAdded?.ToLocalTime(); 
       set => _dateAdded = value; 
   }
}

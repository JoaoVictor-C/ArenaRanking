using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ArenaBackend.Models;

public class OldPlayer
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    // Riot account identifiers
    [BsonElement("puuid")]
    public string Puuid { get; set; } = string.Empty;

    [BsonElement("riot_id")]
    public string RiotId { get; set; } = string.Empty;

    [BsonElement("nome")]
    public string Nome { get; set; } = string.Empty;

    // Ranking information
    [BsonElement("mmr_atual")]
    public int MmrAtual { get; set; } = 1000;

    [BsonElement("delta_mmr")]
    public int DeltaMmr { get; set; } = 0;

    [BsonElement("wins")]
    public int Wins { get; set; } = 0;

    [BsonElement("losses")]
    public int Losses { get; set; } = 0;

    // Match processing data
    [BsonElement("ultimo_match_id_processado")]
    public string UltimoMatchIdProcessado { get; set; } = string.Empty;

    [BsonElement("ultima_atualizacao")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UltimaAtualizacao { get; set; }

    [BsonElement("profile_icon_id")]
    public int ProfileIconId { get; set; } = 0;

    [BsonElement("historico_mmr")]
    public object HistoricoMmr { get; set; } = new { };

    [BsonElement("auto_check")]
    public bool AutoCheck { get; set; } = false;

    // Metadata
    [BsonElement("date_added")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DateAdded { get; set; }

    [BsonElement("last_games")]
    public object LastGames { get; set; } = new { };

    // Computed properties
    [BsonIgnore]
    public int TotalGames => Wins + Losses;

    [BsonIgnore]
    public double WinRate => TotalGames > 0 ? (double)Wins / TotalGames : 0;
}
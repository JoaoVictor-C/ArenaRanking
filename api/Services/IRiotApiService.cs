using ArenaBackend.Models;

namespace ArenaBackend.Services
{
    public interface IRiotApiService
    {
        Task<object?> GetSummonerByPuuid(string puuid);
        Task<string?> GetPlayer(string puuid);
        Task<List<string>?> GetMatchHistoryPuuid(string puuid, int quantity, string type);
        Task<GetMatchDataModel?> GetMatchDetails(string matchId);
        Task<string?> VerifyRiotId(string tagline, string name);
        Task<string?> GetSummonerIdByName(string summonerName);
        Task<object?> GetRankedDataByPuuid(string puuid);
        Task<(string, bool)> ConsultarRiotApi(string riotId);
    }
}
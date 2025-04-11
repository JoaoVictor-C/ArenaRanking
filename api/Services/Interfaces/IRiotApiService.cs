using ArenaBackend.Models;

namespace ArenaBackend.Services
{
    public interface IRiotApiService
    {
        Task<string?> GetTier(string puuid);
        Task<GetRiotIdDataModel?> GetRiotIdByPuuid(string puuid, string region = "americas");
        Task<List<string>?> GetMatchHistoryPuuid(string puuid, int quantity, string type);
        Task<GetMatchDataModel?> GetMatchDetails(string matchId);
        Task<string?> VerifyRiotId(string tagline, string name);
        Task<object?> GetRankedDataByPuuid(string puuid, string region = "americas");
        Task<(string, bool)> ConsultarRiotApi(string riotId);
    }
}
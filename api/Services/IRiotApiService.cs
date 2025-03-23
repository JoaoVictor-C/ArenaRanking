namespace ArenaBackend.Services
{
    public interface IRiotApiService
    {
        Task<object?> GetSummonerByPuuid(string puuid);
        Task<string?> GetPlayer(string puuid);
        Task<List<object>?> GetMatchHistoryPuuid(string puuid);
        Task<object?> GetMatchDetails(string matchId);
        Task<string?> VerifyRiotId(string tagline, string name);
        Task<string?> GetSummonerIdByName(string summonerName);
        Task<object?> GetRankedDataByPuuid(string puuid);
        Task<string> ConsultarRiotApi(string riotId);
    }
}
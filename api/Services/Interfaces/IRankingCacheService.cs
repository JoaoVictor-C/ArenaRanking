using ArenaBackend.Models;

namespace ArenaBackend.Services
{
    public interface IRankingCacheService
    {
        Task<IEnumerable<Player>> GetCachedRankingAsync(int page, int pageSize);
        Task<int> GetTotalPlayersAsync();
        Task RefreshCacheAsync();
        Task<IEnumerable<Player>> GetAllTrackedPlayersAsync(int page, int pageSize);
        Task RefreshAllTrackedPlayersAsync();
        Task<int> GetTotalTrackedPlayersAsync();
    }
}
using ArenaBackend.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArenaBackend.Repositories
{
    public interface IPlayerRepository
    {
        Task<IEnumerable<Player>> GetAllPlayersAsync();
        Task<IEnumerable<Player>> GetAllTrackedPlayersAsync(int page = 1, int pageSize = 100);
        Task<IEnumerable<Player>> GetRanking(int page =1, int pageSize = 100);
        Task<IEnumerable<Player>> GetRankingByRegion(string region, int page = 1, int pageSize = 100);
        Task<IEnumerable<Player>> GetPlayersByServerAsync(string server);
        Task<Player> GetPlayerByIdAsync(string id);
        Task<Player> GetPlayerByPuuidAsync(string puuid);
        Task<List<Player>> GetPlayersByPuuidsAsync(IEnumerable<string> puuids);
        Task<Player> GetPlayerByRiotIdAsync(string gameName, string tagLine);
        Task CreatePlayerAsync(Player player);
        Task CreatePlayersAsync(IEnumerable<Player> players);
        Task UpdatePlayerAsync(Player player);
        Task UpdatePlayersAsync(IEnumerable<Player> players);
        Task DeletePlayerAsync(string id);
        Task UpdateAllPlayerRankingsAsync();
    }
}
using ArenaBackend.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArenaBackend.Repositories
{
    public interface IPlayerRepository
    {
        Task<IEnumerable<Player>> GetAllPlayersAsync();
        Task<IEnumerable<Player>> GetRanking(int page =1, int pageSize = 100);
        Task<Player> GetPlayerByIdAsync(string id);
        Task<Player> GetPlayerByPuuidAsync(string puuid);
        Task<Player> GetPlayerByRiotIdAsync(string gameName, string tagLine);
        Task CreatePlayerAsync(Player player);
        Task CreatePlayersAsync(IEnumerable<Player> players);
        Task UpdatePlayerAsync(Player player);
        Task DeletePlayerAsync(string id);
        Task UpdateAllPlayerRankingsAsync();
    }
}
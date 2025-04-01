using ArenaBackend.Models;

namespace ArenaBackend.Repositories
{
    public interface IPlayerRepository
    {
        Task<IEnumerable<Player>> GetAllPlayersAsync();
        Task<Player> GetPlayerByIdAsync(string id);
        Task<Player> GetPlayerByPuuidAsync(string puuid);
        Task<Player> GetPlayerByRiotIdAsync(string gameName, string tagLine);
        Task CreatePlayerAsync(Player player);
        Task CreatePlayersAsync(IEnumerable<Player> players);
        Task UpdatePlayerAsync(Player player);
        Task DeletePlayerAsync(string id);
    }
}
using ArenaBackend.Models;

namespace ArenaBackend.Repositories
{
    public interface IPlayerRepository
    {
        Task<Player> GetPlayerByIdAsync(string id);
        Task<IEnumerable<Player>> GetAllPlayersAsync();
        Task CreatePlayerAsync(Player Player);
        Task UpdatePlayerAsync(Player Player);
        Task DeletePlayerAsync(string id);
    }
}
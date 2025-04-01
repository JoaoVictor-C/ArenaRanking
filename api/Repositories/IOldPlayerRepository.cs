using ArenaBackend.Models;

namespace ArenaBackend.Repositories
{
    public interface IOldPlayerRepository
    {
        Task<IEnumerable<OldPlayer>> GetAllPlayersAsync();
    }
}
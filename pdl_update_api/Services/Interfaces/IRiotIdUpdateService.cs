using System.Threading.Tasks;

namespace ArenaBackend.Services
{
    public interface IRiotIdUpdateService
    {
        Task UpdateAllPlayersRiotIdsAsync();
    }
}
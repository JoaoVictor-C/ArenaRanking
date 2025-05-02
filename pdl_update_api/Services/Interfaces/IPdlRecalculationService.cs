using System.Threading.Tasks;

namespace ArenaBackend.Services
{
    public interface IPdlRecalculationService
    {
        Task RecalculateAllPlayersPdlAsync();
        Task RecalculatePlayerPdlAsync(string puuid);
    }
}
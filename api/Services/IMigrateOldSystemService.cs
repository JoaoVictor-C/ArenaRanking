namespace ArenaBackend.Services;

public interface IMigrateOldSystemService
{
    Task<bool> MigrateOldPlayers();
}
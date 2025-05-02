using ArenaBackend.Repositories;

namespace ArenaBackend.Factories
{
    public interface IRepositoryFactory
    {
        IPlayerRepository GetPlayerRepository();
    }
}
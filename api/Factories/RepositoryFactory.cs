using ArenaBackend.Configs;
using ArenaBackend.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ArenaBackend.Factories
{
    public class RepositoryFactory : IRepositoryFactory
    {
        private readonly IMongoClient _mongoClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptions<MongoDbSettings> _dbSettings;
        private IPlayerRepository? _playerRepository;

        public RepositoryFactory(
            IMongoClient mongoClient,
            ILoggerFactory loggerFactory,
            IOptions<MongoDbSettings> dbSettings)
        {
            _mongoClient = mongoClient;
            _loggerFactory = loggerFactory;
            _dbSettings = dbSettings;
        }

        public IPlayerRepository GetPlayerRepository()
        {
            // Lazy loading do repositório - criado somente quando necessário e reutilizado
            return _playerRepository ??= new PlayerRepository(
                _mongoClient,
                _loggerFactory.CreateLogger<PlayerRepository>(),
                _dbSettings);
        }
    }
}
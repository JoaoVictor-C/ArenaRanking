using ArenaBackend.Configs;
using ArenaBackend.Repositories;
using ArenaBackend.Services.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ArenaBackend.Factories
{
    public class RepositoryFactory : IRepositoryFactory
    {
        private readonly IMongoClient _mongoClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEnvironmentConfigProvider _configProvider;
        private IPlayerRepository? _playerRepository;

        public RepositoryFactory(
            IMongoClient mongoClient,
            ILoggerFactory loggerFactory,
            IEnvironmentConfigProvider configProvider)
        {
            _mongoClient = mongoClient;
            _loggerFactory = loggerFactory;
            _configProvider = configProvider;
        }

        public IPlayerRepository GetPlayerRepository()
        {

            return _playerRepository ??= new PlayerRepository(
                _mongoClient,
                _loggerFactory.CreateLogger<PlayerRepository>(),
                _configProvider);
        }
    }
}
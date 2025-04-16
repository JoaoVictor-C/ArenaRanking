using ArenaBackend.Models;
using ArenaBackend.Repositories;
using ArenaBackend.Factories;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services
{
    public class RankingCacheService : IRankingCacheService
    {
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly ILogger<RankingCacheService> _logger;
        private readonly object _cacheLock = new object();
        
        private List<Player> _cachedRanking = new List<Player>();
        private List<Player> _cachedAllTrackedPlayers = new List<Player>();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private DateTime _lastAllTrackedUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheValidityDuration = TimeSpan.FromMinutes(5);

        public RankingCacheService(IRepositoryFactory repositoryFactory, ILogger<RankingCacheService> logger)
        {
            _repositoryFactory = repositoryFactory;
            _logger = logger;
        }

        public async Task<IEnumerable<Player>> GetCachedRankingAsync(int page, int pageSize)
        {
            await EnsureCacheIsUpdatedAsync();
            
            // Aplicar paginação nos dados em cache
            lock (_cacheLock)
            {
                return _cachedRanking
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
        }
        
        public async Task<IEnumerable<Player>> GetAllTrackedPlayersAsync(int page, int pageSize)
        {
            await EnsureAllTrackedPlayersAreUpdatedAsync();
            
            lock (_cacheLock)
            {
                return _cachedAllTrackedPlayers
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
        }

        public async Task<int> GetTotalPlayersAsync()
        {
            await EnsureCacheIsUpdatedAsync();
            
            lock (_cacheLock)
            {
                return _cachedRanking.Count;
            }
        }
        
        public async Task<int> GetTotalTrackedPlayersAsync()
        {
            await EnsureAllTrackedPlayersAreUpdatedAsync();
            
            lock (_cacheLock)
            {
                return _cachedAllTrackedPlayers.Count;
            }
        }

        public async Task RefreshCacheAsync()
        {
            try
            {
                _logger.LogInformation("Atualizando cache de ranking...");
                
                // Obter o ranking completo do repositório
                var playerRepository = _repositoryFactory.GetPlayerRepository();
                var players = await playerRepository.GetRanking(page: 1, pageSize: 10000);
                
                // Atualizar o cache atomicamente
                lock (_cacheLock)
                {
                    _cachedRanking = players.ToList();
                    _lastCacheUpdate = DateTime.UtcNow;
                }
                
                _logger.LogInformation($"Cache de ranking atualizado com {_cachedRanking.Count} jogadores");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar o cache de ranking");
            }
        }
        
        public async Task RefreshAllTrackedPlayersAsync()
        {
            try
            {
                _logger.LogInformation("Atualizando cache de todos os jogadores com tracking ativado...");
                
                var playerRepository = _repositoryFactory.GetPlayerRepository();
                var players = await playerRepository.GetAllTrackedPlayersAsync(page: 1, pageSize: 10000);
                
                lock (_cacheLock)
                {
                    _cachedAllTrackedPlayers = players.ToList();
                    _lastAllTrackedUpdate = DateTime.UtcNow;
                }
                
                _logger.LogInformation($"Cache de todos os jogadores com tracking atualizado com {_cachedAllTrackedPlayers.Count} jogadores");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar o cache de todos os jogadores com tracking");
            }
        }

        private async Task EnsureCacheIsUpdatedAsync()
        {
            // Se o cache estiver vazio ou expirado, atualize-o
            if (_cachedRanking.Count == 0 || DateTime.UtcNow - _lastCacheUpdate > _cacheValidityDuration)
            {
                await RefreshCacheAsync();
            }
        }
        
        private async Task EnsureAllTrackedPlayersAreUpdatedAsync()
        {
            // Se o cache estiver vazio ou expirado, atualize-o
            if (_cachedAllTrackedPlayers.Count == 0 || DateTime.UtcNow - _lastAllTrackedUpdate > _cacheValidityDuration)
            {
                await RefreshAllTrackedPlayersAsync();
            }
        }
    }
}
using ArenaBackend.Models;
using ArenaBackend.Repositories;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services
{
    public class RankingCacheService : IRankingCacheService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly ILogger<RankingCacheService> _logger;
        private readonly object _cacheLock = new object();
        
        private List<Player> _cachedRanking = new List<Player>();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheValidityDuration = TimeSpan.FromMinutes(5);

        public RankingCacheService(IPlayerRepository playerRepository, ILogger<RankingCacheService> logger)
        {
            _playerRepository = playerRepository;
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

        public async Task<int> GetTotalPlayersAsync()
        {
            await EnsureCacheIsUpdatedAsync();
            
            lock (_cacheLock)
            {
                return _cachedRanking.Count;
            }
        }

        public async Task RefreshCacheAsync()
        {
            try
            {
                _logger.LogInformation("Atualizando cache de ranking...");
                
                // Obter o ranking completo do repositório
                var players = await _playerRepository.GetRanking(page: 1, pageSize: 10000);
                
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

        private async Task EnsureCacheIsUpdatedAsync()
        {
            // Se o cache estiver vazio ou expirado, atualize-o
            if (_cachedRanking.Count == 0 || DateTime.UtcNow - _lastCacheUpdate > _cacheValidityDuration)
            {
                await RefreshCacheAsync();
            }
        }
    }
}
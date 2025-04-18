using ArenaBackend.Models;
using ArenaBackend.Repositories;
using ArenaBackend.Factories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ArenaBackend.Services
{
    public class RankingCacheService : IRankingCacheService
    {
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly ILogger<RankingCacheService> _logger;
        private readonly object _cacheLock = new object();
        
        private List<Player> _cachedRanking = new List<Player>();
        private List<Player> _cachedAllTrackedPlayers = new List<Player>();
        private readonly ConcurrentDictionary<string, Player> _playerCache = new ConcurrentDictionary<string, Player>();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private DateTime _lastAllTrackedUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheValidityDuration = TimeSpan.FromMinutes(5);
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public RankingCacheService(IRepositoryFactory repositoryFactory, ILogger<RankingCacheService> logger)
        {
            _repositoryFactory = repositoryFactory;
            _logger = logger;
        }

        public async Task<Player> GetPlayerByPuuidAsync(string puuid)
        {
            // Verifica se o jogador está no cache local
            if (_playerCache.TryGetValue(puuid, out Player player))
            {
                return player;
            }
            
            // Busca o jogador no cache de jogadores rastreados
            var cachedPlayer = _cachedAllTrackedPlayers.FirstOrDefault(p => p.Puuid == puuid);
            if (cachedPlayer != null)
            {
                // Adiciona ao cache local
                _playerCache[puuid] = cachedPlayer;
                return cachedPlayer;
            }
            
            // Se não encontrou, busca do banco de dados
            var playerRepository = _repositoryFactory.GetPlayerRepository();
            var dbPlayer = await playerRepository.GetPlayerByPuuidAsync(puuid);
            
            if (dbPlayer != null)
            {
                // Adiciona ao cache local
                _playerCache[puuid] = dbPlayer;
            }
            
            return dbPlayer;
        }
        
        public async Task<List<Player>> GetPlayersByPuuidsAsync(IEnumerable<string> puuids)
        {
            var result = new List<Player>();
            var puuidsToFetch = new List<string>();
            
            // Primeiro verifica no cache local
            foreach (var puuid in puuids)
            {
                if (_playerCache.TryGetValue(puuid, out Player player))
                {
                    result.Add(player);
                }
                else
                {
                    puuidsToFetch.Add(puuid);
                }
            }
            
            if (!puuidsToFetch.Any())
            {
                return result;
            }
            
            // Depois procura no cache de jogadores rastreados
            foreach (var player in _cachedAllTrackedPlayers)
            {
                if (puuidsToFetch.Contains(player.Puuid))
                {
                    result.Add(player);
                    puuidsToFetch.Remove(player.Puuid);
                    // Adiciona ao cache local
                    _playerCache[player.Puuid] = player;
                }
            }
            
            if (!puuidsToFetch.Any())
            {
                return result;
            }
            
            // Por fim, busca os jogadores restantes do banco
            var playerRepository = _repositoryFactory.GetPlayerRepository();
            var dbPlayers = await playerRepository.GetPlayersByPuuidsAsync(puuidsToFetch);
            
            foreach (var player in dbPlayers)
            {
                result.Add(player);
                // Adiciona ao cache local
                _playerCache[player.Puuid] = player;
            }
            
            return result;
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
            // Evita múltiplas atualizações simultâneas
            if (!await _refreshLock.WaitAsync(0))
            {
                _logger.LogDebug("Atualização de cache já em andamento, ignorando solicitação adicional");
                return;
            }
            
            try
            {
                _logger.LogInformation("Atualizando cache de ranking...");
                
                // Obter o ranking completo do repositório
                var playerRepository = _repositoryFactory.GetPlayerRepository();
                var players = await playerRepository.GetRanking(page: 1, pageSize: 10000);
                var playersList = players.ToList();
                
                // Atualizar o cache atomicamente
                lock (_cacheLock)
                {
                    _cachedRanking = playersList;
                    _lastCacheUpdate = DateTime.UtcNow;
                    
                    // Atualiza também o cache local
                    foreach (var player in playersList)
                    {
                        _playerCache[player.Puuid] = player;
                    }
                }
                
                _logger.LogInformation($"Cache de ranking atualizado com {_cachedRanking.Count} jogadores");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar o cache de ranking");
            }
            finally
            {
                _refreshLock.Release();
            }
        }
        
        public async Task RefreshAllTrackedPlayersAsync()
        {
            // Evita múltiplas atualizações simultâneas
            if (!await _refreshLock.WaitAsync(0))
            {
                _logger.LogDebug("Atualização de cache de jogadores já em andamento, ignorando solicitação adicional");
                return;
            }
            
            try
            {
                _logger.LogInformation("Atualizando cache de todos os jogadores com tracking ativado...");
                
                var playerRepository = _repositoryFactory.GetPlayerRepository();
                var players = await playerRepository.GetAllTrackedPlayersAsync(page: 1, pageSize: 10000);
                var playersList = players.ToList();
                
                lock (_cacheLock)
                {
                    _cachedAllTrackedPlayers = playersList;
                    _lastAllTrackedUpdate = DateTime.UtcNow;
                    
                    // Atualiza também o cache local
                    foreach (var player in playersList)
                    {
                        _playerCache[player.Puuid] = player;
                    }
                }
                
                _logger.LogInformation($"Cache de todos os jogadores com tracking atualizado com {_cachedAllTrackedPlayers.Count} jogadores");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar o cache de todos os jogadores com tracking");
            }
            finally
            {
                _refreshLock.Release();
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
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;
using ArenaBackend.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArenaBackend.Repositories
{
    public class PlayerRepository : IPlayerRepository
    {
        private readonly IMongoCollection<Player> _players;
        private readonly ILogger<PlayerRepository> _logger;
        private readonly TimeSpan _bulkUpdateLockTimeout = TimeSpan.FromMinutes(5);
        private readonly object _bulkUpdateLock = new object();
        private bool _isIndexesCreated = false;

        public PlayerRepository(
            IMongoClient client, 
            ILogger<PlayerRepository> logger,
            IOptions<MongoDbSettings> settings)
        {
            var dbSettings = settings.Value;
            var databaseName = dbSettings.IsDevelopment 
                ? $"{dbSettings.DatabaseName}{dbSettings.TestDatabaseSuffix}"
                : dbSettings.DatabaseName;

            var database = client.GetDatabase(databaseName);
            _players = database.GetCollection<Player>("player");
            _logger = logger;
            
            // Índices são criados de forma assíncrona na primeira chamada
            _ = EnsureIndexesExistAsync();
        }

        private async Task EnsureIndexesExistAsync()
        {
            if (_isIndexesCreated) return;
            
            try
            {
                // Cria ou garante que os índices existam
                var indexKeys = Builders<Player>.IndexKeys;
                
                // Índice para consultas de ranking (usado frequentemente)
                await _players.Indexes.CreateOneAsync(
                    new CreateIndexModel<Player>(
                        indexKeys.Ascending(p => p.RankPosition).Ascending(p => p.TrackingEnabled),
                        new CreateIndexOptions { Background = true, Name = "ranking_lookup" }
                    )
                );
                
                // Índice para puuid (usado em muitas consultas)
                await _players.Indexes.CreateOneAsync(
                    new CreateIndexModel<Player>(
                        indexKeys.Ascending(p => p.Puuid),
                        new CreateIndexOptions { Background = true, Name = "puuid_lookup" }
                    )
                );
                
                // Índice composto para GameName e TagLine (usados juntos)
                await _players.Indexes.CreateOneAsync(
                    new CreateIndexModel<Player>(
                        indexKeys.Ascending(p => p.GameName).Ascending(p => p.TagLine),
                        new CreateIndexOptions { Background = true, Name = "riotid_lookup" }
                    )
                );
                
                // Índice para região e servidor
                await _players.Indexes.CreateOneAsync(
                    new CreateIndexModel<Player>(
                        indexKeys.Ascending(p => p.Region).Ascending(p => p.Server),
                        new CreateIndexOptions { Background = true, Name = "region_server_lookup" }
                    )
                );
                
                // Índice para campo TrackingEnabled (consultas de jogadores rastreados)
                await _players.Indexes.CreateOneAsync(
                    new CreateIndexModel<Player>(
                        indexKeys.Ascending(p => p.TrackingEnabled),
                        new CreateIndexOptions { Background = true, Name = "tracking_lookup" }
                    )
                );
                
                _isIndexesCreated = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar índices no MongoDB");
            }
        }

        public async Task<IEnumerable<Player>> GetAllPlayersAsync()
        {
            // Usando projeção para excluir campos grandes desnecessários para listagens
            var projection = Builders<Player>.Projection
                .Exclude(p => p.MatchStats.RecentGames);
                
            return await _players.Find(player => true)
                .Project<Player>(projection)
                .ToListAsync();    
        } 

        public async Task<IEnumerable<Player>> GetRanking(int page = 1, int pageSize = 100)
        {
            try
            {
                // Usar projeção para excluir campos grandes
                var projection = Builders<Player>.Projection
                    .Include(p => p.Id)
                    .Include(p => p.Puuid)
                    .Include(p => p.GameName)
                    .Include(p => p.TagLine)
                    .Include(p => p.ProfileIconId)
                    .Include(p => p.Region)
                    .Include(p => p.Server)
                    .Include(p => p.Pdl)
                    .Include(p => p.RankPosition)
                    .Include(p => p.LastPlacement)
                    .Include(p => p.MatchStats.Win)
                    .Include(p => p.MatchStats.Loss)
                    .Include(p => p.MatchStats.AveragePlacement)
                    .Include(p => p.MatchStats.ChampionsPlayed);

                // Filtro otimizado: corrigido para não usar operações aritméticas
                var filter = Builders<Player>.Filter.And(
                    Builders<Player>.Filter.Eq(p => p.TrackingEnabled, true)
                   // Builders<Player>.Filter.Or(
                    //    Builders<Player>.Filter.Gt(p => p.MatchStats.Win, 0),
                    //    Builders<Player>.Filter.Gt(p => p.MatchStats.Loss, 0)
                   // )
                );
                
                return await _players
                    .Find(filter)
                    .Project<Player>(projection)
                    .Sort(Builders<Player>.Sort.Ascending(p => p.RankPosition))
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ranking");
                throw;
            }
        }

        public async Task<IEnumerable<Player>> GetRankingByRegion(string region, int page = 1, int pageSize = 100)
        {
            try
            {
                // Usar projeção como na função anterior
                var projection = Builders<Player>.Projection
                    .Include(p => p.Id)
                    .Include(p => p.Puuid)
                    .Include(p => p.GameName)
                    .Include(p => p.TagLine)
                    .Include(p => p.ProfileIconId)
                    .Include(p => p.Region)
                    .Include(p => p.Server)
                    .Include(p => p.Pdl)
                    .Include(p => p.RankPosition)
                    .Include(p => p.LastPlacement)
                    .Include(p => p.MatchStats.Win)
                    .Include(p => p.MatchStats.Loss)
                    .Include(p => p.MatchStats.AveragePlacement)
                    .Include(p => p.MatchStats.ChampionsPlayed);

                // Filtro composto otimizado, corrigido para não usar operações aritméticas
                var filter = Builders<Player>.Filter.And(
                    Builders<Player>.Filter.Eq(p => p.TrackingEnabled, true),
                    Builders<Player>.Filter.Or(
                        Builders<Player>.Filter.Gt(p => p.MatchStats.Win, 0),
                        Builders<Player>.Filter.Gt(p => p.MatchStats.Loss, 0)
                    ),
                    Builders<Player>.Filter.Eq(p => p.Region, region)
                );
                
                return await _players
                    .Find(filter)
                    .Project<Player>(projection)
                    .Sort(Builders<Player>.Sort.Ascending(p => p.RankPosition))
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ranking by region");
                throw;
            }
        }

        public async Task<IEnumerable<Player>> GetAllTrackedPlayersAsync(int page = 1, int pageSize = 100)
        {
            try
            {
                return await _players
                    .Find(player => player.TrackingEnabled == true)
                    .SortByDescending(player => player.Pdl)
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all tracked players");
                throw;
            }
        }

        public async Task<IEnumerable<Player>> GetPlayersByServerAsync(string server)
        {
            var projection = Builders<Player>.Projection
                .Exclude(p => p.MatchStats.RecentGames);
                
            return await _players
                .Find(player => player.Server == server)
                .Project<Player>(projection)
                .ToListAsync();
        }

        public async Task<Player> GetPlayerByIdAsync(string id)
        {
            return await _players.Find(player => player.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Player> GetPlayerByPuuidAsync(string puuid)
        {
            return await _players.Find(player => player.Puuid == puuid).FirstOrDefaultAsync();
        }

        public async Task<List<Player>> GetPlayersByPuuidsAsync(IEnumerable<string> puuids)
        {
            if (puuids == null || !puuids.Any())
                return new List<Player>();
                
            var filter = Builders<Player>.Filter.In(p => p.Puuid, puuids);
            return await _players.Find(filter).ToListAsync();
        }

        public async Task<Player> GetPlayerByRiotIdAsync(string gameName, string tagLine)
        {
            return await _players.Find(player => player.TagLine == tagLine && player.GameName == gameName).FirstOrDefaultAsync();
        }

        public async Task CreatePlayerAsync(Player player)
        {
            // Verify if player already exists
            var existingPlayer = await GetPlayerByRiotIdAsync(player.GameName, player.TagLine);
            if (existingPlayer != null)
            {
                return;
            }
            // If player does not exist, create a new one
            await _players.InsertOneAsync(player);
        }

        public async Task CreatePlayersAsync(IEnumerable<Player> players)
        {
            if (players == null || !players.Any())
                return;
                
            await _players.InsertManyAsync(players);
        }

        public async Task UpdatePlayerAsync(Player player)
        {
            await _players.ReplaceOneAsync(p => p.Id == player.Id, player);
        }

        public async Task UpdatePlayersAsync(IEnumerable<Player> players)
        {
            if (players == null || !players.Any())
                return;
                
            var writes = new List<WriteModel<Player>>();
            
            foreach (var player in players)
            {
                writes.Add(new ReplaceOneModel<Player>(
                    Builders<Player>.Filter.Eq(p => p.Id, player.Id), 
                    player));
            }
            
            if (writes.Count > 0)
            {
                await _players.BulkWriteAsync(writes);
            }
        }

        public async Task DeletePlayerAsync(string id)
        {
            await _players.DeleteOneAsync(player => player.Id == id);
        }

        public async Task UpdateAllPlayerRankingsAsync()
        {
            try
            {
                var filterBuilder = Builders<Player>.Filter;
                var filter = filterBuilder.And(
                    filterBuilder.Eq(p => p.TrackingEnabled, true),
                    filterBuilder.Or(
                        filterBuilder.Gt(p => p.MatchStats.Win, 0),
                        filterBuilder.Gt(p => p.MatchStats.Loss, 0)
                    )
                );
                
                var allPlayers = await _players
                    .Find(filter)
                    .Project<Player>(Builders<Player>.Projection
                        .Include(p => p.Id)
                        .Include(p => p.Puuid)
                        .Include(p => p.Pdl))
                    .SortByDescending(player => player.Pdl)
                    .ToListAsync();

                // Atribuir posições de ranking
                var bulkUpdates = new List<WriteModel<Player>>();
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    var player = allPlayers[i];
                    int newRank = i + 1; // Ranking começa em 1
                    
                    var updateDef = Builders<Player>.Update.Set(p => p.RankPosition, newRank);
                    bulkUpdates.Add(new UpdateOneModel<Player>(
                        Builders<Player>.Filter.Eq(p => p.Id, player.Id),
                        updateDef
                    ));
                    
                    // Processa em lotes para evitar pacotes muito grandes
                    if (bulkUpdates.Count >= 500 || i == allPlayers.Count - 1)
                    {
                        if (bulkUpdates.Count > 0)
                        {
                            await _players.BulkWriteAsync(bulkUpdates);
                            bulkUpdates.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na atualização do ranking de jogadores");
                throw;
            }
        }
    }
}
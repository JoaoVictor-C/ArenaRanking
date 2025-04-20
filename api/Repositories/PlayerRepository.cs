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
        }

        public async Task<IEnumerable<Player>> GetAllPlayersAsync()
        {
            return await _players.Find(player => true).ToListAsync();    
        } 

        public async Task<IEnumerable<Player>> GetRanking(int page = 1, int pageSize = 100)
        {
            try
            {
                return await _players
                    //.Find(player => player.TrackingEnabled == true && player.MatchStats.Win + player.MatchStats.Loss > 0)
                    .Find(player => player.TrackingEnabled == true)
                    .SortBy(player => player.RankPosition)
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
                return await _players
                    .Find(player => player.TrackingEnabled == true && player.MatchStats.Win + player.MatchStats.Loss > 0 && player.Region == region)
                    .SortBy(player => player.RankPosition)
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
            return await _players.Find(player => player.Server == server).ToListAsync();
        }

        public async Task<Player> GetPlayerByIdAsync(string id)
        {
            return await _players.Find(player => player.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Player> GetPlayerByPuuidAsync(string puuid)
        {
            return await _players.Find(player => player.Puuid == puuid).FirstOrDefaultAsync();
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
            await _players.InsertManyAsync(players);
        }

        public async Task UpdatePlayerAsync(Player player)
        {
            await _players.ReplaceOneAsync(p => p.Id == player.Id, player);
        }

        public async Task DeletePlayerAsync(string id)
        {
            await _players.DeleteOneAsync(player => player.Id == id);
        }

        public async Task UpdateAllPlayerRankingsAsync()
        {
            // Buscar jogadores ordenados por PDL (decrescente)
            var allPlayers = await _players
                .Find(player => player.TrackingEnabled == true && player.MatchStats.Win + player.MatchStats.Loss > 0)
                .SortByDescending(player => player.Pdl)
                .ToListAsync();

            // Atribuir posições de ranking
            for (int i = 0; i < allPlayers.Count; i++)
            {
                var player = allPlayers[i];
                player.RankPosition = i + 1; // Ranking começa em 1
                await _players.ReplaceOneAsync(p => p.Id == player.Id, player);
            }
        }
    }
}

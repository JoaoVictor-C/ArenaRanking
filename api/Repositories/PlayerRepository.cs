using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;

namespace ArenaBackend.Repositories
{
    public class PlayerRepository : IPlayerRepository
    {
        private readonly IMongoCollection<Player> _players;

        public PlayerRepository(IMongoClient client)
        {
            var database = client.GetDatabase("arena_rank");
            //d rop
            // database.DropCollection("player");
            _players = database.GetCollection<Player>("player");
        }

        public async Task<IEnumerable<Player>> GetAllPlayersAsync()
        {
            return await _players.Find(player => true).ToListAsync();    
        } 

        public async Task<IEnumerable<Player>> GetRanking(int page = 1, int pageSize = 200)
        {
            return await _players
                .Find(player => player.TrackingEnabled == true && player.MatchStats.Win + player.MatchStats.Loss > 0)
                .SortBy(player => player.RankPosition)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<IEnumerable<Player>> GetRanking()
        {
            return await _players
                .Find(player => player.TrackingEnabled == true && player.MatchStats.Win + player.MatchStats.Loss > 0)
                .SortByDescending(player => player.Pdl)
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
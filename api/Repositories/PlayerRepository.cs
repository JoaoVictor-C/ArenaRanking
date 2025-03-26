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
            // drop player
            //database.DropCollection("player");
            _players = database.GetCollection<Player>("player");
        }

        public async Task<IEnumerable<Player>> GetAllPlayersAsync()
        {
            return await _players.Find(player => true).ToListAsync();    
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
            await _players.InsertOneAsync(player);
        }

        public async Task UpdatePlayerAsync(Player player)
        {
            await _players.ReplaceOneAsync(p => p.Id == player.Id, player);
        }

        public async Task DeletePlayerAsync(string id)
        {
            await _players.DeleteOneAsync(player => player.Id == id);
        }
    }
}
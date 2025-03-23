using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;

namespace ArenaBackend.Repositories
{
    public class PlayerRepository : IPlayerRepository
    {
        private readonly IMongoCollection<Player> _Players;

        public PlayerRepository(IMongoClient client)
        {
            var database = client.GetDatabase("arena_ranking");
            _Players = database.GetCollection<Player>("players");
        }

        public async Task<IEnumerable<Player>> GetAllPlayersAsync()
        {
            return await _Players.Find(Player => true).ToListAsync();
        }

        public async Task<Player> GetPlayerByIdAsync(string id)
        {
            return await _Players.Find(Player => Player.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreatePlayerAsync(Player Player)
        {
            await _Players.InsertOneAsync(Player);
        }

        public async Task UpdatePlayerAsync(Player Player)
        {
            await _Players.ReplaceOneAsync(a => a.Id == Player.Id, Player);
        }

        public async Task DeletePlayerAsync(string id)
        {
            await _Players.DeleteOneAsync(Player => Player.Id == id);
        }
    }
}
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;

namespace ArenaBackend.Repositories
{
    public class OldPlayerRepository : IOldPlayerRepository
    {
        private readonly IMongoCollection<OldPlayer> _players;

        public OldPlayerRepository(IMongoClient client)
        {
            var database = client.GetDatabase("arena_ranking");
            _players = database.GetCollection<OldPlayer>("players");
        }

        public async Task<IEnumerable<OldPlayer>> GetAllPlayersAsync()
        {
            return await _players.Find(player => player.AutoCheck == true && player.MmrAtual > 770).ToListAsync();
        }
    }
}
using MongoDB.Driver;
using ArenaBackend.Models;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services
{
    public class DatabaseMigrationService
    {
        private readonly IMongoCollection<Player> _players;
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(IMongoClient client, ILogger<DatabaseMigrationService> logger)
        {
            var database = client.GetDatabase("arena_rank");
            _players = database.GetCollection<Player>("player");
            _logger = logger;
        }

        public async Task MigrateRegionFields()
        {
            try
            {
                var update = Builders<Player>.Update
                    .SetOnInsert(p => p.Region, "americas")
                    .SetOnInsert(p => p.Server, "br1");

                var result = await _players.UpdateManyAsync(
                    filter: Builders<Player>.Filter.Or(
                        Builders<Player>.Filter.Exists(p => p.Region, false),
                        Builders<Player>.Filter.Exists(p => p.Server, false)
                    ),
                    update: update
                );

                _logger.LogInformation($"Migration completed. Modified {result.ModifiedCount} documents.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during migration");
                throw;
            }
        }
    }
}
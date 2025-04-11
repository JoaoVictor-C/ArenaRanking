using MongoDB.Driver;
using ArenaBackend.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ArenaBackend.Configs;

namespace ArenaBackend.Services
{
    public class DatabaseMigrationService
    {
        private readonly IMongoCollection<Player> _players;
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(IMongoClient client, 
            ILogger<DatabaseMigrationService> logger,
            IOptions<MongoDbSettings> settings)
        {
            var dbSettings = settings.Value;
            var databaseName = dbSettings.IsDevelopment 
                ? $"{dbSettings.DatabaseName}{dbSettings.TestDatabaseSuffix}"
                : dbSettings.DatabaseName;

            var database = client.GetDatabase(databaseName);
            _players = database.GetCollection<Player>("player");
            _logger = logger;
            _logger.LogInformation($"Using database: {databaseName}");
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

        public async Task MigrateRecentGamesFields()
        {
            try
            {
                var filter = Builders<Player>.Filter.Where(p => 
                    p.MatchStats != null && 
                    p.MatchStats.RecentGames != null && 
                    p.MatchStats.RecentGames.Any(g => g.Players == null || !g.Players.Any()));

                var players = await _players.Find(filter).ToListAsync();

                foreach (var player in players)
                {
                    var updatedRecentGames = new List<DetailedMatch>();
                    foreach (var game in player.MatchStats.RecentGames)
                    {
                        // Converter jogos antigos para o novo formato
                        if (game.Players == null || !game.Players.Any())
                        {
                            var newDetailedMatch = new DetailedMatch
                            {
                                MatchId = game.MatchId,
                                GameCreation = game.GameCreation,
                                Players = new List<PlayerDTO>()
                            };
                            updatedRecentGames.Add(newDetailedMatch);
                        }
                        else
                        {
                            updatedRecentGames.Add(game);
                        }
                    }

                    player.MatchStats.RecentGames = updatedRecentGames;

                    var updateDefinition = Builders<Player>.Update
                        .Set(p => p.MatchStats.RecentGames, updatedRecentGames);

                    await _players.UpdateOneAsync(
                        Builders<Player>.Filter.Eq(p => p.Id, player.Id),
                        updateDefinition
                    );
                }

                _logger.LogInformation($"Recent games migration completed. Updated {players.Count} players.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recent games migration");
                throw;
            }
        }
    }
}
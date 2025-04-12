using MongoDB.Driver;
using ArenaBackend.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ArenaBackend.Configs;
using MongoDB.Bson;

namespace ArenaBackend.Services;

public class DatabaseMigrationService
{
    private readonly IMongoCollection<Player> _players;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly string _databaseName;

    public DatabaseMigrationService(
        IMongoClient client,
        ILogger<DatabaseMigrationService> logger,
        IOptions<MongoDbSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);

        var dbSettings = settings.Value;
        _databaseName = dbSettings.IsDevelopment 
            ? $"{dbSettings.DatabaseName}{dbSettings.TestDatabaseSuffix}"
            : dbSettings.DatabaseName;

        var database = client.GetDatabase(_databaseName);
        _players = database.GetCollection<Player>("player");
        _logger = logger;
        
        _logger.LogInformation("Initialized DatabaseMigrationService using database: {DatabaseName}", _databaseName);
    }

    public async Task MigrateRegionFields()
    {
        try
        {
            var filter = Builders<Player>.Filter.Or(
                Builders<Player>.Filter.Exists(p => p.Region, false),
                Builders<Player>.Filter.Exists(p => p.Server, false)
            );

            var update = Builders<Player>.Update
                .SetOnInsert(p => p.Region, "americas")
                .SetOnInsert(p => p.Server, "br1");

            var result = await _players.UpdateManyAsync(filter, update);

            _logger.LogInformation("Region fields migration completed. Modified {Count} documents", 
                result.ModifiedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate region fields");
            throw;
        }
    }

    public async Task MigrateRecentGamesField()
    {
        try
        {
            _logger.LogInformation("Starting recentGames field migration");

            // Get all players
            var filterReset = Builders<Player>.Filter.Empty;

            var updateReset = Builders<Player>.Update
                .Set("matchStats.recentGames", new List<DetailedMatch>());

            // Atualiza os documentos que não possuem o campo recentGames
            var result = await _players.UpdateManyAsync(filterReset, updateReset);
            _logger.LogInformation("Updated {Count} documents to initialize recentGames field", 
                result.ModifiedCount);
            _logger.LogInformation("RecentGames field migration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate recentGames field");
            throw;
        }
    }
}
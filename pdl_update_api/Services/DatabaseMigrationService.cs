using MongoDB.Driver;
using ArenaBackend.Models;
using Microsoft.Extensions.Logging;
using ArenaBackend.Configs;
using MongoDB.Bson;
using ArenaBackend.Services.Configuration;

namespace ArenaBackend.Services;

public class DatabaseMigrationService
{
    private readonly IMongoCollection<Player> _players;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly string _databaseName;

    public DatabaseMigrationService(
        IMongoClient client,
        ILogger<DatabaseMigrationService> logger,
        IEnvironmentConfigProvider configProvider)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configProvider);

        var dbSettings = configProvider.GetMongoDbSettings();
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

            //var filterReset = Builders<Player>.Filter.Empty;
            // Get only players with tracking enabled false
            var filterReset = Builders<Player>.Filter.Eq(p => p.TrackingEnabled, false);
            var updateReset = Builders<Player>.Update
                .Set("matchStats.recentGames", new List<DetailedMatch>());

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
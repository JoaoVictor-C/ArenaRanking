using MongoDB.Driver;
using ArenaBackend.Configs;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using ArenaBackend.Services.Configuration;

namespace ArenaBackend.Services;

public class DatabaseCloneService
{
    private readonly IMongoClient _client;
    private readonly MongoDbSettings _settings;
    private readonly ILogger<DatabaseCloneService> _logger;

    public DatabaseCloneService(
        IMongoClient client,
        IEnvironmentConfigProvider configProvider,
        ILogger<DatabaseCloneService> logger)
    {
        _client = client;
        _settings = configProvider.GetMongoDbSettings();
        _logger = logger;
    }

    public async Task CloneProductionToTest()
    {
        try
        {
            var sourceDbName = _settings.DatabaseName;
            var targetDbName = $"{sourceDbName}{_settings.TestDatabaseSuffix}";

            await _client.DropDatabaseAsync(targetDbName);

            var sourceDb = _client.GetDatabase(sourceDbName);
            var collections = await (await sourceDb.ListCollectionNamesAsync()).ToListAsync();

            var targetDb = _client.GetDatabase(targetDbName);

            foreach (var collectionName in collections)
            {
                var sourceCollection = sourceDb.GetCollection<BsonDocument>(collectionName);
                var filter = collectionName == "player" 
                    ? Builders<BsonDocument>.Filter.Eq("trackingEnabled", true)
                    : new BsonDocument();
                
                var documents = await sourceCollection.Find(filter).ToListAsync();

                var targetCollection = targetDb.GetCollection<BsonDocument>(collectionName);
                if (documents.Count > 0)
                {
                    await targetCollection.InsertManyAsync(documents);
                }

                _logger.LogInformation("Collection {CollectionName} cloned with {DocumentCount} documents", collectionName, documents.Count);
            }

            _logger.LogInformation("Database {DatabaseName} cloned successfully", targetDbName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao clonar banco de dados");
            throw;
        }
    }
}
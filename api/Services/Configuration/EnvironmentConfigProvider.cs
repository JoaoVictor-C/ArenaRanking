using Microsoft.Extensions.Logging;
using ArenaBackend.Configs;

namespace ArenaBackend.Services.Configuration
{
    public interface IEnvironmentConfigProvider
    {
        MongoDbSettings GetMongoDbSettings();
        RiotApiSettings GetRiotApiSettings();
        PdlCalculationSettings GetPdlCalculationSettings();
    }

    public class EnvironmentConfigProvider : IEnvironmentConfigProvider
    {
        private readonly ILogger<EnvironmentConfigProvider> _logger;

        public EnvironmentConfigProvider(ILogger<EnvironmentConfigProvider> logger)
        {
            _logger = logger;
        }

        public MongoDbSettings GetMongoDbSettings()
        {
            var settings = new MongoDbSettings
            {
                ConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? string.Empty,
                DatabaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME") ?? string.Empty,
                IsDevelopment = IsEnvironmentDevelopment(),
                TestDatabaseSuffix = Environment.GetEnvironmentVariable("MONGODB_TEST_DB_SUFFIX") ?? "_test"
            };

            _logger.LogInformation("Configuração MongoDB carregada. Database: {DatabaseName}, IsDev: {IsDev}", 
                settings.DatabaseName, settings.IsDevelopment);

            return settings;
        }

        public RiotApiSettings GetRiotApiSettings()
        {
            var settings = new RiotApiSettings
            {
                ApiKey = Environment.GetEnvironmentVariable("RIOT_API_KEY") ?? string.Empty,
            };

            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                _logger.LogWarning("RIOT_API_KEY não foi definida no ambiente");
            }

            return settings;
        }

        public PdlCalculationSettings GetPdlCalculationSettings()
        {
            var settings = new PdlCalculationSettings();
            _logger.LogInformation("Configurações de cálculo de PDL carregadas.");
            return settings;
        }

        private bool IsEnvironmentDevelopment()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            return environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        }
    }
}

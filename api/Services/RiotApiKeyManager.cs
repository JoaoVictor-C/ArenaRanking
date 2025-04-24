using Microsoft.Extensions.Logging;
using ArenaBackend.Services.Configuration;

namespace ArenaBackend.Services
{
    public class RiotApiKeyManager : IRiotApiKeyManager
    {
        private readonly string _apiKey;
        private readonly ILogger<RiotApiKeyManager> _logger;

        public RiotApiKeyManager(
            IEnvironmentConfigProvider configProvider, 
            ILogger<RiotApiKeyManager> logger)
        {
            _logger = logger;
            _apiKey = configProvider.GetRiotApiSettings().ApiKey;
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("RIOT_API_KEY não foi definida no ambiente");
            }
        }

        public string GetApiKey()
        {
            return _apiKey;
        }
    }
}

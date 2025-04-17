using ArenaBackend.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using ArenaBackend.Models;
using System.Net;
using System.Text;

namespace ArenaBackend.Services
{

    public class RiotApiKeyManager : IRiotApiKeyManager
    {
        private string _apiKey;

        public RiotApiKeyManager(IOptions<RiotApiSettings> settings)
        {
            Console.WriteLine($"RiotApiKeyManager: {Environment.NewLine}Settings: {settings.Value.ApiKey}");
            _apiKey = settings.Value.ApiKey;
        }

        public string GetApiKey()
        {
            Console.WriteLine($"RiotApiKeyManager: {Environment.NewLine}ApiKey: {_apiKey}");
            return _apiKey;
        }

        public void UpdateApiKey(string newApiKey)
        {
            if (!string.IsNullOrEmpty(newApiKey))
            {
                _apiKey = newApiKey;
                // Save on appsettings.json:
                string appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                var json = File.ReadAllText(appSettingsPath);
                dynamic jsonObj = JsonConvert.DeserializeObject(json);
                jsonObj["RiotApiSettings"]["ApiKey"] = newApiKey;
                string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                File.WriteAllText(appSettingsPath, output);
            }
        }
    }
}
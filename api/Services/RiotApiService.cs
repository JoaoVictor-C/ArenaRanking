using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ArenaBackend.Configs;
using ArenaBackend.Models;
using ArenaBackend.Repositories;
using ArenaBackend.Factories;

namespace ArenaBackend.Services
{
    public class RiotApiService : IRiotApiService
    {
        private readonly IRiotApiKeyManager _apiKeyManager;
        private readonly ILogger<RiotApiService> _logger;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private const int RATE_LIMIT_DELAY_MS = 121000; // 2 minutes 1 second

        public RiotApiService(
            IRiotApiKeyManager apiKeyManager, 
            ILogger<RiotApiService> logger,
            IRepositoryFactory repositoryFactory,
            IHttpClientFactory httpClientFactory)
        {
            _apiKeyManager = apiKeyManager;
            _logger = logger;
            _repositoryFactory = repositoryFactory;
            _httpClientFactory = httpClientFactory;
        }
        
        private HttpClient GetConfiguredHttpClient()
        {
            var client = _httpClientFactory.CreateClient("RiotApi");
            client.DefaultRequestHeaders.Add("X-Riot-Token", _apiKeyManager.GetApiKey());
            return client;
        }

        public async Task<GetRiotIdDataModel?> GetPuuidByRiotId(string gameName, string tagLine, string region = "americas")
        {
            // Escape de componentes individuais antes de montar a URL
            string escapedGameName = Uri.EscapeDataString(gameName);
            string escapedTagLine = Uri.EscapeDataString(tagLine);
            string baseUrl = $"https://{region}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/";
            string url = baseUrl + $"{escapedGameName}/{escapedTagLine}";
            
            var responseJson = await MakeApiRequest<Dictionary<string, object>>(url, $"Riot ID {gameName}#{tagLine}");
            if (responseJson == null) return null;
            if (responseJson.TryGetValue("puuid", out var puuid))
            {
                return new GetRiotIdDataModel
                {
                    GameName = gameName,
                    TagLine = tagLine,
                    Puuid = puuid.ToString()
                };
            }
            return null;
        }

        public async Task<GetRiotIdDataModel?> GetRiotIdByPuuid(string puuid, string region = "americas")
        {
            // Escape de componentes individuais antes de montar a URL
            string escapedPuuid = Uri.EscapeDataString(puuid);
            string baseUrl = $"https://{region}.api.riotgames.com/riot/account/v1/accounts/by-puuid/";
            string url = baseUrl + escapedPuuid;
            
            var responseJson = await MakeApiRequest<Dictionary<string, object>>(url, $"Riot ID by puuid {puuid}");
            if (responseJson == null) return null;
            if (responseJson.TryGetValue("gameName", out var gameName) && 
                responseJson.TryGetValue("tagLine", out var tagLine))
            {
                return new GetRiotIdDataModel
                {
                    GameName = gameName.ToString(),
                    TagLine = tagLine.ToString(),
                    Puuid = puuid
                };
            }
            return null;
        }

        public async Task<string?> GetTier(string puuid, string server)
        {
            string url = $"https://{server}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}";
            
            var data = await MakeApiRequest<List<Dictionary<string, object>>>(url, $"Ranked data for puuid {puuid}");
            if (data == null) return "UNRANKED";
            
            foreach (var entry in data)
            {
                if (entry.TryGetValue("queueType", out var queueType) && queueType.ToString() == "RANKED_SOLO_5x5")
                {
                    if (entry.TryGetValue("tier", out var tier))
                    {
                        return tier.ToString();
                    }
                }
            }
            
            return "UNRANKED";
        }

        public async Task<object?> GetRankedDataByPuuid(string puuid, string region = "americas")
        {
            string url = $"https://{region}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}";
            return await MakeApiRequest<object>(url, $"ranked data for puuid {puuid}");
        }

        public async Task<List<string>?> GetMatchHistoryPuuid(string puuid, int quantity, string type)
        {
            var playerRepository = _repositoryFactory.GetPlayerRepository();
            var player = await playerRepository.GetPlayerByPuuidAsync(puuid);
            if (player == null) return null;

            string url = $"https://{player.Region}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?type={type}&start=0&count={quantity}";
            return await MakeApiRequest<List<string>>(url, $"match history for puuid {puuid}");
        }

        public async Task<GetMatchDataModel?> GetMatchDetails(string matchId)
        {
            string region = matchId.Split('_')[0].ToLower();
            string baseRegion = GetBaseRegion(region);

            string url = $"https://{baseRegion}.api.riotgames.com/lol/match/v5/matches/{matchId}";
            return await MakeApiRequest<GetMatchDataModel>(url, $"match details for matchId {matchId}");
        }

        private string GetBaseRegion(string serverRegion)
        {
            return serverRegion.ToLower() switch
            {
                "br1" or "la1" or "la2" or "na1" => "americas",
                "eun1" or "euw1" or "tr1" or "ru" => "europe",
                "kr" or "jp1" => "asia",
                "oc1" or "ph2" or "sg2" or "th2" or "tw2" or "vn2" => "sea",
                _ => "americas"
            };
        }

        public async Task<string?> VerifyRiotId(string tagline, string name)
        {
            if (string.IsNullOrEmpty(tagline) || string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Riot ID ou Tagline inválido.");
                return null;
            }
            // Search for the user to get the region
            var playerRepository = _repositoryFactory.GetPlayerRepository();
            Player player = await playerRepository.GetPlayerByRiotIdAsync(name, tagline);


            string region = player?.Region ?? "americas";
            if (string.IsNullOrEmpty(region))
            {
                _logger.LogWarning($"Região não encontrada para o jogador {name}#{tagline}");
                return null;
            }
            string url = $"https://{region}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{name}/{tagline}";
            
            var responseJson = await MakeApiRequest<Dictionary<string, object>>(url, $"Riot ID {name}#{tagline}");
            return responseJson?.TryGetValue("puuid", out var puuid) == true ? puuid.ToString() : null;
        }

        public async Task<(string, bool)> ConsultarRiotApi(string riotId)
        {
            try
            {
                string[] parts = riotId.Split('#');
                if (parts.Length != 2)
                {
                    return ("Formato de Riot ID inválido. Use o formato nome#tagline.", false);
                }
                
                string name = parts[0];
                string tagline = parts[1];
                
                string? puuid = await VerifyRiotId(tagline, name);
                if (puuid == null)
                {
                    return ($"Jogador {riotId} não encontrado na API da Riot.", false);
                }
                
                return (puuid, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro inesperado ao consultar a Riot API para {riotId}");
                return ("Erro inesperado ao consultar a API da Riot.", false);
            }
        }

        private async Task<T?> MakeApiRequest<T>(string url, string resourceDescription) where T : class 
        {
            try
            {
                // Change " " to "%20" in the URL
                url = url.Replace(" ", "%20");
                
                var httpClient = GetConfiguredHttpClient();
                HttpResponseMessage response = await httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var jsonDeserialized = JsonConvert.DeserializeObject<T>(jsonResponse);

                    return jsonDeserialized;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Not found: {resourceDescription}");
                    return null;
                }
                else if (response.StatusCode == (HttpStatusCode)403)
                {
                    _logger.LogError($"Forbidden while fetching {resourceDescription}. Check your Riot API key.");
                    return null;
                }
                else if (response.StatusCode == (HttpStatusCode)429)
                {
                    _logger.LogWarning($"Rate limit exceeded while fetching {resourceDescription}. Waiting before retry...");
                    await Task.Delay(RATE_LIMIT_DELAY_MS);
                    return await MakeApiRequest<T>(url, resourceDescription);
                }
                else
                {
                    _logger.LogError($"Error fetching {resourceDescription}: Status Code {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Request exception while fetching {resourceDescription}: {ex.Message}");
                return null;
            }
        }

    }
}

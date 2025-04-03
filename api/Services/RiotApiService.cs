using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ArenaBackend.Configs;
using ArenaBackend.Models;

namespace ArenaBackend.Services
{
    public class RiotApiService : IRiotApiService
    {
        private readonly RiotApiSettings _riotApiSettings;
        private readonly ILogger<RiotApiService> _logger;
        private const string REGION = "americas";
        private const string REGION2 = "br1";
        private const int RATE_LIMIT_DELAY_MS = 121000; // 2 minutes 1 second

        public RiotApiService(IOptions<RiotApiSettings> riotApiSettings, ILogger<RiotApiService> logger)
        {
            _riotApiSettings = riotApiSettings.Value;
            _logger = logger;
            
        }
        
        private HttpClient ConfigureHttpClient()
        {
            var _httpClient = new HttpClient();

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _riotApiSettings.ApiKey);
            return _httpClient;
        }

        public async Task<object?> GetSummonerByPuuid(string puuid)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/summoner/v4/summoners/by-puuid/{puuid}";
            return await MakeApiRequest<object>(url, $"summoner data for puuid {puuid}");
        }

        public async Task<string?> GetSummonerIdByName(string summonerName)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/summoner/v4/summoners/by-name/{summonerName}";
            
            var responseJson = await MakeApiRequest<Dictionary<string, object>>(url, $"Summoner ID by name {summonerName}");
            return responseJson?.TryGetValue("id", out var id) == true ? id.ToString() : null;
        }

        public async Task<GetRiotIdDataModel?> GetRiotIdByPuuid(string puuid)
        {
            string url = $"https://{REGION}.api.riotgames.com/riot/account/v1/accounts/by-puuid/{puuid}";
            
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


        public async Task<string?> GetTier(string puuid)
        {
            string url = $"https://{REGION2}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}";
            
            var data = await MakeApiRequest<List<Dictionary<string, object>>>(url, $"Ranked data for puuid {puuid}");
            if (data == null) return null;
            
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

        public async Task<object?> GetRankedDataByPuuid(string puuid)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}";
            return await MakeApiRequest<object>(url, $"ranked data for puuid {puuid}");
        }


        public async Task<List<string>?> GetMatchHistoryPuuid(string puuid, int quantity, string type)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?{type}=normal&start=0&count={quantity}";
            return await MakeApiRequest<List<string>>(url, $"match history for puuid {puuid}");
        }

        public async Task<GetMatchDataModel?> GetMatchDetails(string matchId)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/match/v5/matches/{matchId}";
            return await MakeApiRequest<GetMatchDataModel>(url, $"match details for matchId {matchId}");
        }

        public async Task<string?> VerifyRiotId(string tagline, string name)
        {
            name = name.Replace(" ", "%20");
            string url = $"https://{REGION}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{name}/{tagline}";
            
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
                var _httpClient = ConfigureHttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                
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
                else if (response.StatusCode == (HttpStatusCode)403) // Forbidden, normally for riot api key issues
                {
                    _logger.LogError($"Forbidden while fetching {resourceDescription}. Check your Riot API key.");
                    return null;
                }
                else if (response.StatusCode == (HttpStatusCode)429) // Rate Limit
                {
                    _logger.LogWarning($"Rate limit exceeded while fetching {resourceDescription}. Waiting before retry...");
                    await Task.Delay(RATE_LIMIT_DELAY_MS);
                    return await MakeApiRequest<T>(url, resourceDescription); // Retry
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

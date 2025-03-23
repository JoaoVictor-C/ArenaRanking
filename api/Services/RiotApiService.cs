using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ArenaBackend.Configs;
using api.Models;

namespace ArenaBackend.Services
{
    public class RiotApiService : IRiotApiService
    {
        private readonly RiotApiSettings _riotApiSettings;
        private readonly ILogger<RiotApiService> _logger;
        private const string REGION = "americas";
        private const int RATE_LIMIT_DELAY_MS = 10000;

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

        #region Summoner Endpoints
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
        #endregion

        #region League Endpoints
        public async Task<string?> GetPlayer(string puuid)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}";
            
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
        #endregion

        #region Match Endpoints
        public async Task<List<object>?> GetMatchHistoryPuuid(string puuid)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?type=normal&start=0&count=5";
            return await MakeApiRequest<List<object>>(url, $"match history for puuid {puuid}");
        }

        public async Task<dynamic> GetMatchDetails(string matchId)
        {
            string url = $"https://{REGION}.api.riotgames.com/lol/match/v5/matches/{matchId}";
            return await MakeApiRequest<GetMatchDataModel>(url, $"match details for matchId {matchId}");
        }
        #endregion

        #region Account Endpoints
        public async Task<string?> VerifyRiotId(string tagline, string name)
        {
            name = name.Replace(" ", "%20");
            string url = $"https://{REGION}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{name}/{tagline}";
            
            var responseJson = await MakeApiRequest<Dictionary<string, object>>(url, $"Riot ID {name}#{tagline}");
            return responseJson?.TryGetValue("puuid", out var puuid) == true ? puuid.ToString() : null;
        }
        #endregion

        public async Task<string> ConsultarRiotApi(string riotId)
        {
            try
            {
                Console.WriteLine(riotId);
                string[] parts = riotId.Split('#');
                if (parts.Length != 2)
                {
                    return "Formato de Riot ID inválido. Use o formato nome#tagline.";
                }
                
                string name = parts[0];
                string tagline = parts[1];
                
                string? puuid = await VerifyRiotId(tagline, name);
                Console.WriteLine(puuid);
                if (puuid == null)
                {
                    return $"Jogador {riotId} não encontrado na Riot API.";
                }
                
                var matches = await GetMatchHistoryPuuid(puuid);
                if (matches != null && matches.Count > 0)
                {
                    var latestMatch = matches[0];
                    return $"Jogador {riotId} encontrado. Última partida: {latestMatch}";
                }
                else
                {
                    return $"O jogador {riotId} não tem partidas recentes.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro inesperado ao consultar a Riot API para {riotId}");
                return "Ocorreu um erro inesperado ao consultar o jogador.";
            }
        }

        #region Helper Methods
        private async Task<T?> MakeApiRequest<T>(string url, string resourceDescription) where T : class 
        {
            try
            {
                var _httpClient = ConfigureHttpClient();
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Success");
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var jsonDeserialized = JsonConvert.DeserializeObject<T>(jsonResponse);

                    return jsonDeserialized;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Not found: {resourceDescription}");
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
        #endregion
    }
}

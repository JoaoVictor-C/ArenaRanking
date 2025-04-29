using ArenaBackend.Models;
using ArenaBackend.Repositories;
using Microsoft.Extensions.Logging;
using ArenaBackend.Factories;
using ArenaBackend.Services.Configuration;
using ArenaBackend.Configs;

namespace ArenaBackend.Services
{
    public class PdlHandlerService : IPdlHandlerService
    {
        private readonly ILogger<PdlHandlerService> _logger;
        private readonly IRiotApiService _riotApiService;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IPlayerRepository _playerRepository;
        private readonly PdlCalculationSettings _pdlSettings;

        public PdlHandlerService(
           IRepositoryFactory repositoryFactory,
           ILogger<PdlHandlerService> logger,
           IRiotApiService riotApiService,
           IEnvironmentConfigProvider configProvider)
        {
            _repositoryFactory = repositoryFactory;
            _logger = logger;
            _riotApiService = riotApiService;
            _playerRepository = _repositoryFactory.GetPlayerRepository();
            _pdlSettings = configProvider.GetPdlCalculationSettings();
        }

        public async Task<bool> ProcessPlayerPdlAsync(Player playerData)
        {
            if (!playerData.TrackingEnabled)
            {
                return false;
            }

            string puuid = playerData.Puuid;
            string gameName = playerData.GameName;
            string tagLine = playerData.TagLine;
            DateTime? dateAdded = playerData.DateAdded;
            DateTime? lastUpdate = playerData.LastUpdate;

            if (DateTime.UtcNow - lastUpdate < TimeSpan.FromMinutes(15))
            {
                return false;
            }

            string lastMatchId = playerData.MatchStats.LastProcessedMatchId;

            var matchIds = await _riotApiService.GetMatchHistoryPuuid(puuid, 30, "normal");
            if (matchIds == null || matchIds.Count == 0)
            {
                _logger.LogWarning("Could not retrieve match history for player {GameName}#{TagLine}", gameName, tagLine);
                return false;
            }

            var player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
            var newMatchIds = GetNewMatches(matchIds, lastMatchId);
            if (newMatchIds.Count == 0)
            {
                player.LastUpdate = DateTime.UtcNow;
                await _playerRepository.UpdatePlayerAsync(player);
                return true;
            }

            newMatchIds.Reverse();
            foreach (var matchId in newMatchIds)
            {
                await ProcessMatchAsync(matchId, puuid, dateAdded);
            }

            return true;
        }

        private static List<string> GetNewMatches(List<string> allMatches, string lastProcessedMatchId)
        {
            if (string.IsNullOrEmpty(lastProcessedMatchId))
            {
                return allMatches;
            }

            var newMatchIds = new List<string>();
            foreach (var matchId in allMatches)
            {
                if (matchId == lastProcessedMatchId)
                {
                    break;
                }
                newMatchIds.Add(matchId);
            }

            return newMatchIds;
        }

        public async Task<bool> ProcessMatchAsync(string matchId, string puuid, DateTime? dateAdded)
        {
            GetMatchDataModel? matchDetails = await _riotApiService.GetMatchDetails(matchId);
            if (matchDetails == null)
            {
                _logger.LogWarning($"Could not retrieve details for match {matchId}");
                return false;
            }

            if (matchDetails.info.gameMode != "CHERRY")
            {
                var player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
                if (player != null)
                {
                    player.MatchStats.LastProcessedMatchId = matchId;
                    player.LastUpdate = DateTime.UtcNow;
                    await _playerRepository.UpdatePlayerAsync(player);
                }
                return false;
            }

            DateTime gameCreationDate = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(matchDetails.info.gameCreation)).UtcDateTime;

            long gameCreation = gameCreationDate.Ticks;
            if (dateAdded.HasValue && gameCreation < dateAdded.Value.ToUniversalTime().Ticks)
            {
                var player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
                player.MatchStats.LastProcessedMatchId = matchId;
                player.LastUpdate = DateTime.UtcNow;
                await _playerRepository.UpdatePlayerAsync(player);
                return false;
            }

            var participantsPuuids = matchDetails.info.participants.Select(p => p.puuid).ToList();
            var existingPlayers = new List<Player>();

            foreach (var participantPuuid in participantsPuuids)
            {
                var player = await _playerRepository.GetPlayerByPuuidAsync(participantPuuid);
                if (player != null)
                {
                    existingPlayers.Add(player);
                }
            }

            var existingPlayerDict = existingPlayers.ToDictionary(p => p.Puuid);

            int totalPdl = 0;
            int totalPlayers = participantsPuuids.Count;
            var puuidsToProcess = new List<string>();
            var gameNames = new Dictionary<string, string>();
            var tagLines = new Dictionary<string, string>();
            var placements = new Dictionary<string, int>();
            var currentPdls = new Dictionary<string, int>();
            var win = new Dictionary<string, int>();
            var loss = new Dictionary<string, int>();
            var championsPlayed = new Dictionary<string, List<Dictionary<string, string>>>();
            var profileIcon = new Dictionary<string, int>();
            var playerDTOs = new List<PlayerDTO>();

            foreach (var participant in matchDetails.info.participants)
            {
                int placement = participant.placement;
                bool isWin = placement <= 4;

                if (existingPlayerDict.TryGetValue(participant.puuid, out var player))
                {
                    int pdl = player.Pdl;
                    int playerWin = player.MatchStats.Win;
                    int playerLoss = player.MatchStats.Loss;

                    if (player.MatchStats.LastProcessedMatchId == matchId || (player.TrackingEnabled && participant.puuid != puuid))
                    {
                        totalPdl += pdl;
                        continue;
                    }

                    puuidsToProcess.Add(participant.puuid);
                    gameNames[participant.puuid] = player.GameName;
                    tagLines[participant.puuid] = player.TagLine;
                    placements[participant.puuid] = placement;
                    currentPdls[participant.puuid] = pdl;
                    win[participant.puuid] = playerWin + (isWin ? 1 : 0);
                    loss[participant.puuid] = playerLoss + (isWin ? 0 : 1);
                    profileIcon[participant.puuid] = participant.profileIcon;

                    var championEntry = new ChampionPlayed
                    {
                        ChampionId = participant.championId.ToString(),
                        ChampionName = participant.championName
                    };

                    if (player.MatchStats.ChampionsPlayed == null)
                    {
                        player.MatchStats.ChampionsPlayed = new List<ChampionPlayed>();
                    }

                    if (player.MatchStats.ChampionsPlayed.Count < 4)
                    {
                        player.MatchStats.ChampionsPlayed.Add(championEntry);
                    }
                    else
                    {
                        player.MatchStats.ChampionsPlayed.RemoveAt(0);
                        player.MatchStats.ChampionsPlayed.Add(championEntry);
                    }

                    championsPlayed[participant.puuid] = player.MatchStats.ChampionsPlayed
                       .Select(c => new Dictionary<string, string> { { c.ChampionId, c.ChampionName } })
                       .ToList();

                    totalPdl += pdl;
                }
                else
                {
                    string server = matchId.Split('_')[0];
                    string region = GetBaseRegion(server);
                    string? tier = await _riotApiService.GetTier(participant.puuid, server);
                    if (string.IsNullOrEmpty(tier))
                    {
                        _logger.LogWarning($"Could not retrieve tier for {participant.riotIdGameName}#{participant.riotIdTagline}");
                        tier = "UNRANKED";
                    }
                    int pdl = GetDefaultPdlForTier(tier);

                    puuidsToProcess.Add(participant.puuid);
                    gameNames[participant.puuid] = participant.riotIdGameName;
                    tagLines[participant.puuid] = participant.riotIdTagline;
                    placements[participant.puuid] = placement;
                    currentPdls[participant.puuid] = pdl;
                    win[participant.puuid] = isWin ? 1 : 0;
                    loss[participant.puuid] = isWin ? 0 : 1;
                    profileIcon[participant.puuid] = participant.profileIcon;
                    championsPlayed[participant.puuid] = new List<Dictionary<string, string>>
               {
                  new Dictionary<string, string> { { $"{participant.championId}", participant.championName  } }
               };

                    totalPdl += pdl;

                    await AddPlayerAsync(participant.puuid, participant.riotIdGameName, participant.riotIdTagline, false, pdl, region, server);
                }

                var augments = new List<string>();
                var items = new List<int>();
                for (int i = 1; i <= 6; i++)
                {
                    var augmentProperty = typeof(GetMatchDataModel.Info.ParticipantesInfo).GetProperty($"playerAugment{i}");
                    if (augmentProperty != null)
                    {
                        var augmentValue = (int)augmentProperty.GetValue(participant);
                        if (augmentValue > 0)
                        {
                            augments.Add(augmentValue.ToString());
                        }
                    }
                }
                for (int i = 0; i <= 6; i++)
                {
                    var itemProperty = typeof(GetMatchDataModel.Info.ParticipantesInfo).GetProperty($"item{i}");
                    if (itemProperty != null)
                    {
                        var itemValue = (int)itemProperty.GetValue(participant);
                        if (itemValue > 0)
                        {
                            items.Add(itemValue);
                        }
                    }
                }
                if (augments.Count == 0)
                {
                    augments = null;
                }
                if (items.Count == 0)
                {
                    items = null;
                }

                var playerDTO = new PlayerDTO
                {
                    GameName = participant.riotIdGameName,
                    TagLine = participant.riotIdTagline,
                    ChampionId = participant.championId,
                    ChampionName = participant.championName,
                    Placement = participant.placement,
                    Augments = augments ?? new List<string>(),
                    Items = items ?? new List<int>(),
                    Kills = participant.kills,
                    Deaths = participant.deaths,
                    Assists = participant.assists,
                    TotalDamageDealt = participant.totalDamageDealt,
                    IsCurrentPlayer = participant.puuid == puuid
                };

                playerDTOs.Add(playerDTO);
            }

            int averagePdl = totalPlayers > 0 ? totalPdl / totalPlayers : _pdlSettings.DefaultPdl;

            foreach (var playerPuuid in puuidsToProcess)
            {
                int pdlChange = CalculatePdlChange(
                   currentPdls[playerPuuid],
                   averagePdl,
                   placements[playerPuuid],
                   win[playerPuuid] + loss[playerPuuid]);

                int finalPdl = currentPdls[playerPuuid] + pdlChange;

                await UpdatePlayerPdlAsync(
                   playerPuuid,
                   finalPdl,
                   matchId,
                   win.TryGetValue(playerPuuid, out var playerWin) ? playerWin : 0,
                   loss.TryGetValue(playerPuuid, out var playerLoss) ? playerLoss : 0,
                   championsPlayed.TryGetValue(playerPuuid, out var playerChampions) ? playerChampions : new List<Dictionary<string, string>>(),
                   placements.TryGetValue(playerPuuid, out var playerPlacement) ? playerPlacement : 0,
                   profileIcon.TryGetValue(playerPuuid, out var playerProfileIcon) ? playerProfileIcon : 0,
                   matchDetails.info,
                   playerDTOs
                );

                var playerData = await _playerRepository.GetPlayerByPuuidAsync(playerPuuid);
                if (playerData != null && playerData.TrackingEnabled)
                {
                    _logger.LogInformation($"Player {gameNames[playerPuuid]}#{tagLines[playerPuuid]}: Placement {placements[playerPuuid]}, " +
                                     $"PDL {currentPdls[playerPuuid]} -> {finalPdl} (Δ{pdlChange})");
                }
            }

            return true;
        }

        public int CalculatePdlChange(int playerPdl, int averagePdl, int placement, int matchesPlayed)
        {
            float k;
            if (matchesPlayed < _pdlSettings.MinMatchesStable)
            {
                k = _pdlSettings.FactorNewPlayer;
            }
            else
            {
                if (averagePdl == 0)
                {
                    k = _pdlSettings.FactorBase;
                }
                else
                {
                    int pdlDiff = Math.Abs(playerPdl - averagePdl);
                    k = (float)(_pdlSettings.FactorBase + Math.Min(_pdlSettings.FactorMax - _pdlSettings.FactorBase,
                          (10 / (1 + Math.Log10(1 + Math.Abs(pdlDiff)))) * Math.Abs(Math.Tanh(pdlDiff / 4.0f))));

                    if (playerPdl > averagePdl && placement > 6)
                    {
                        k *= 1.1f;
                    }
                    else if (playerPdl < averagePdl && placement <= 2)
                    {
                        k *= 1.15f;
                    }
                }
            }

            if (placement > 4 && playerPdl <= 3000)
            {
                k = Math.Max(40, k - (placement - 4) * 10);
            }

            float multiplier = _pdlSettings.PlacementMultipliers.ContainsKey(placement) ?
               _pdlSettings.PlacementMultipliers[placement] : 0;

            int pdlChange = (int)(k * multiplier);

            return Math.Max(-100, Math.Min(100, pdlChange));
        }

        public async Task<bool> UpdatePlayerPdlAsync(string puuid, int newPdl, string lastMatchId, int win, int loss,
           List<Dictionary<string, string>> championsPlayed, int placement, int profileIcon, GetMatchDataModel.Info matchInfo, List<PlayerDTO> playerDTO = null)
        {
            try
            {
                var player = await _playerRepository.GetPlayerByPuuidAsync(puuid);

                if (player != null)
                {
                    var detailedMatch = new DetailedMatch
                    {
                        MatchId = lastMatchId,
                        Players = playerDTO ?? new List<PlayerDTO>(),
                        GameCreation = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(matchInfo.gameCreation)).UtcDateTime,
                        GameDuration = matchInfo.gameDuration,
                    };

                    if (!player.MatchStats.RecentGames.Any(g => g.MatchId == lastMatchId))
                    {
                        player.MatchStats.RecentGames.Add(detailedMatch);
                        while (player.MatchStats.RecentGames.Count > 10)
                        {
                            player.MatchStats.RecentGames = player.MatchStats.RecentGames
                               .OrderByDescending(g => g.GameCreation)
                               .Take(10)
                               .ToList();
                        }
                    }

                    player.Pdl = newPdl;
                    player.MatchStats.LastProcessedMatchId = lastMatchId;
                    player.LastUpdate = DateTime.UtcNow;
                    player.MatchStats.Win = win;
                    player.MatchStats.Loss = loss;
                    player.LastPlacement = placement;
                    player.ProfileIconId = profileIcon;

                    player.MatchStats.ChampionsPlayed = championsPlayed.Select(dict =>
                    {
                        var entry = dict.First();
                        return new ChampionPlayed
                        {
                            ChampionId = entry.Key,
                            ChampionName = entry.Value
                        };
                    }).ToList();

                    int totalGames = player.MatchStats.Win + player.MatchStats.Loss;
                    if (player.MatchStats.AveragePlacement == 0)
                    {
                        player.MatchStats.AveragePlacement = placement;
                    }
                    else
                    {
                        player.MatchStats.AveragePlacement =
                           ((player.MatchStats.AveragePlacement * (totalGames - 1)) + placement) / totalGames;
                    }

                    await _playerRepository.UpdatePlayerAsync(player);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating PDL for {puuid}: {ex.Message}");
                return false;
            }
        }

        public async Task ProcessAllPlayersPdlAsync()
        {
            _logger.LogInformation("Starting PDL processing for all players...");

            var allPlayers = await _playerRepository.GetAllTrackedPlayersAsync();

            if (!allPlayers.Any())
            {
                _logger.LogInformation("No players found for PDL processing.");
                return;
            }

            int processedCount = 0;
            int totalPlayers = allPlayers.Count();

            foreach (var player in allPlayers)
            {
                try
                {
                    await ProcessPlayerPdlAsync(player);

                    processedCount++;
                    if (processedCount % 5 == 0 || processedCount == totalPlayers)
                    {
                        _logger.LogInformation("PDL processing progress: {Processed}/{Total} players",
                           processedCount, totalPlayers);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing PDL for player {GameName}#{TagLine}",
                    player.GameName, player.TagLine);
                }
            }

            await _playerRepository.UpdateAllPlayerRankingsAsync();

            _logger.LogInformation("PDL processing for all players completed. Processed {Count} players.", processedCount);
        }

        private int GetDefaultPdlForTier(string tier)
        {
            return tier switch
            {
                "IRON" => 800,
                "BRONZE" => 900,
                "SILVER" => 1000,
                "GOLD" => 1200,
                "PLATINUM" => 1500,
                "EMERALD" => 2000,
                "DIAMOND" => 2500,
                "MASTER" => 3000,
                "GRANDMASTER" => 3500,
                "CHALLENGER" => 4000,
                _ => _pdlSettings.DefaultPdl
            };
        }

        private async Task<bool> AddPlayerAsync(string puuid, string gameName, string tagLine, bool trackingEnabled, int pdl, string region, string server)
        {
            try
            {
                var player = new Player
                {
                    Puuid = puuid,
                    GameName = gameName,
                    TagLine = tagLine,
                    Pdl = pdl,
                    TrackingEnabled = trackingEnabled,
                    DateAdded = DateTime.UtcNow,
                    Region = region,
                    Server = server,
                    MatchStats = new MatchStats()
                };

                await _playerRepository.CreatePlayerAsync(player);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding new player {gameName}#{tagLine}: {ex.Message}");
                return false;
            }
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
    }
}

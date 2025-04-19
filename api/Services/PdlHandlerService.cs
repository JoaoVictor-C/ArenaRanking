using ArenaBackend.Models;
using ArenaBackend.Repositories;
using Microsoft.Extensions.Logging;
using ArenaBackend.Factories;
using System.Collections.Concurrent;

namespace ArenaBackend.Services
{
   public class PdlHandlerService : IPdlHandlerService, IDisposable
   {
      private readonly ILogger<PdlHandlerService> _logger;
      private readonly IRiotApiService _riotApiService;
      private readonly IRepositoryFactory _repositoryFactory;
      private readonly IRankingCacheService _rankingCacheService;

      // Constants
      private const int K_FACTOR_BASE = 50;
      private const int K_FACTOR_NEW_PLAYER = 80;
      private const int K_MAX = 140;
      private const int MIN_MATCHES_STABLE = 10;
      private const int DEFAULT_PDL = 1000;

      // Placement multipliers
      private readonly Dictionary<int, float> PLACEMENT_MULTIPLIERS = new Dictionary<int, float>
      {
         { 1, 1.3f },
         { 2, 1.1f },
         { 3, 0.8f },
         { 4, 0.6f },
         { 5, -0.4f },
         { 6, -0.6f },
         { 7, -1.0f },
         { 8, -1.7f }
      };

      // Local caches
      private readonly ConcurrentDictionary<string, GetMatchDataModel> _matchCache = new ConcurrentDictionary<string, GetMatchDataModel>();
      private readonly ConcurrentDictionary<string, DateTime> _lastProcessedTimeByPuuid = new ConcurrentDictionary<string, DateTime>();
      private readonly TimeSpan _minTimeBetweenProcessing = TimeSpan.FromMinutes(15);
      private readonly SemaphoreSlim _throttler;

      public PdlHandlerService(
         IRepositoryFactory repositoryFactory,
         ILogger<PdlHandlerService> logger,
         IRiotApiService riotApiService,
         IRankingCacheService rankingCacheService)
      {
         _repositoryFactory = repositoryFactory;
         _logger = logger;
         _riotApiService = riotApiService;
         _rankingCacheService = rankingCacheService;
         _throttler = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
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

         if (_lastProcessedTimeByPuuid.TryGetValue(puuid, out DateTime lastProcessed))
         {
            if (DateTime.UtcNow - lastProcessed < _minTimeBetweenProcessing)
            {
               return false;
            }
         }

         if (DateTime.UtcNow - lastUpdate < _minTimeBetweenProcessing)
         {
            return false;
         }

         string lastMatchId = playerData.MatchStats.LastProcessedMatchId;

         var matchIds = await _riotApiService.GetMatchHistoryPuuid(puuid, 10, "normal");
         if (matchIds == null || matchIds.Count == 0)
         {
            _logger.LogWarning("Could not retrieve match history for player {GameName}#{TagLine}", gameName, tagLine);
            return false;
         }

         var player = await GetPlayerFromCacheOrRepositoryByPuuidAsync(puuid);

         var newMatchIds = GetNewMatches(matchIds, lastMatchId);
         if (newMatchIds.Count == 0)
         {
            player.LastUpdate = DateTime.UtcNow;
            await UpdatePlayerAsync(player);
            _lastProcessedTimeByPuuid[puuid] = DateTime.UtcNow;
            return true;
         }

         newMatchIds.Reverse();
         foreach (var matchId in newMatchIds)
         {
            await ProcessMatchAsync(matchId, puuid, dateAdded);
         }

         _lastProcessedTimeByPuuid[puuid] = DateTime.UtcNow;
         return true;
      }

      private async Task<Player> GetPlayerFromCacheOrRepositoryByPuuidAsync(string puuid)
      {
         try
         {
            // Primeiro, tente buscar diretamente do serviço de cache
            return await _rankingCacheService.GetPlayerByPuuidAsync(puuid);
         }
         catch (Exception ex)
         {
            _logger.LogWarning($"Erro acessando cache para jogador {puuid}: {ex.Message}");
            
            // Se falhar, busque diretamente do repositório
            var playerRepository = _repositoryFactory.GetPlayerRepository();
            return await playerRepository.GetPlayerByPuuidAsync(puuid);
         }
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
         GetMatchDataModel? matchDetails;
         if (!_matchCache.TryGetValue(matchId, out matchDetails))
         {
            matchDetails = await _riotApiService.GetMatchDetails(matchId);
            if (matchDetails != null)
            {
               _matchCache[matchId] = matchDetails;
            }
         }

         if (matchDetails == null)
         {
            _logger.LogWarning($"Could not retrieve details for match {matchId}");
            return false;
         }

         if (matchDetails.info.gameMode != "CHERRY")
         {
            var player = await GetPlayerFromCacheOrRepositoryByPuuidAsync(puuid);
            if (player != null)
            {
               player.MatchStats.LastProcessedMatchId = matchId;
               player.LastUpdate = DateTime.UtcNow;
               await UpdatePlayerAsync(player);
            }
            return false;
         }

         DateTime gameCreationDate = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(matchDetails.info.gameCreation)).UtcDateTime;

         long gameCreation = gameCreationDate.Ticks;
         if (dateAdded.HasValue && gameCreation < dateAdded.Value.ToUniversalTime().Ticks)
         {
            var player = await GetPlayerFromCacheOrRepositoryByPuuidAsync(puuid);
            player.MatchStats.LastProcessedMatchId = matchId;
            player.LastUpdate = DateTime.UtcNow;
            await UpdatePlayerAsync(player);
            return false;
         }

         var participantsPuuids = matchDetails.info.participants.Select(p => p.puuid).ToList();
         var existingPlayers = new List<Player>();

         var cachedPlayers = await GetPlayersFromCacheByPuuidsAsync(participantsPuuids);
         existingPlayers.AddRange(cachedPlayers);

         var missingPuuids = participantsPuuids.Except(existingPlayers.Select(p => p.Puuid)).ToList();
         if (missingPuuids.Any())
         {
            var playerRepositoryInstance = _repositoryFactory.GetPlayerRepository();
            foreach (var participantPuuid in missingPuuids)
            {
               var player = await playerRepositoryInstance.GetPlayerByPuuidAsync(participantPuuid);
               if (player != null)
               {
                  existingPlayers.Add(player);
               }
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

         int averagePdl = totalPlayers > 0 ? totalPdl / totalPlayers : DEFAULT_PDL;

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

            var playerData = await GetPlayerFromCacheOrRepositoryByPuuidAsync(playerPuuid);
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
         if (matchesPlayed < MIN_MATCHES_STABLE)
         {
            k = K_FACTOR_NEW_PLAYER;
         }
         else
         {
            if (averagePdl == 0)
            {
               k = K_FACTOR_BASE;
            }
            else
            {
               int pdlDiff = Math.Abs(playerPdl - averagePdl);
               k = (float)(K_FACTOR_BASE + Math.Min(K_MAX - K_FACTOR_BASE,
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

         float multiplier = PLACEMENT_MULTIPLIERS.ContainsKey(placement) ?
            PLACEMENT_MULTIPLIERS[placement] : 0;

         int pdlChange = (int)(k * multiplier);

         return Math.Max(-100, Math.Min(100, pdlChange));
      }

      public async Task<bool> UpdatePlayerPdlAsync(string puuid, int newPdl, string lastMatchId, int win, int loss,
         List<Dictionary<string, string>> championsPlayed, int placement, int profileIcon, GetMatchDataModel.Info matchInfo, List<PlayerDTO> playerDTO = null)
      {
         try
         {
            var player = await GetPlayerFromCacheOrRepositoryByPuuidAsync(puuid);

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

               await UpdatePlayerAsync(player);
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

         IEnumerable<Player> allPlayers;
         try 
         {
            allPlayers = await _rankingCacheService.GetAllTrackedPlayersAsync(1, 10000);
            _logger.LogInformation("Using cached players for PDL processing");
         }
         catch (Exception ex)
         {
            _logger.LogWarning($"Failed to get players from cache: {ex.Message}. Falling back to repository.");
            var playerRepository = _repositoryFactory.GetPlayerRepository();
            allPlayers = await playerRepository.GetAllPlayersAsync();
         }

         if (!allPlayers.Any())
         {
            _logger.LogInformation("No players found for PDL processing.");
            return;
         }

         var activePlayers = allPlayers.Where(p => p.TrackingEnabled).ToList();
         _logger.LogInformation($"Processing PDL for {activePlayers.Count} active players");

         var playerBatches = SplitIntoBatches(activePlayers, 20);
         var updatedPlayers = new ConcurrentBag<Player>();
         
         foreach (var batch in playerBatches)
         {
            var tasks = new List<Task<Player>>();
            
            foreach (var player in batch)
            {
               tasks.Add(ProcessPlayerWithThrottlingAndGetUpdatedAsync(player));
            }
            
            var processedPlayers = await Task.WhenAll(tasks);
            
            // Coleta jogadores atualizados para atualização em lote
            foreach (var player in processedPlayers)
            {
               if (player != null)
               {
                  updatedPlayers.Add(player);
               }
            }
            
            // A cada 100 jogadores processados, faz atualização em lote no banco
            if (updatedPlayers.Count >= 5)
            {
               var playersToUpdate = updatedPlayers.ToList();
               updatedPlayers = new ConcurrentBag<Player>();
               
               try
               {
                  var playerRepository = _repositoryFactory.GetPlayerRepository();
                  await playerRepository.UpdatePlayersAsync(playersToUpdate);
                  _logger.LogInformation($"Atualizados {playersToUpdate.Count} jogadores em lote");
               }
               catch (Exception ex)
               {
                  _logger.LogError(ex, $"Erro ao atualizar jogadores em lote: {ex.Message}");
               }
            }
            
            await Task.Delay(500);
         }
         
         // Atualiza os jogadores restantes
         if (updatedPlayers.Count > 0)
         {
            try
            {
               var playerRepository = _repositoryFactory.GetPlayerRepository();
               await playerRepository.UpdatePlayersAsync(updatedPlayers);
               _logger.LogInformation($"Atualizados {updatedPlayers.Count} jogadores restantes em lote");
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, $"Erro ao atualizar jogadores restantes em lote: {ex.Message}");
            }
         }

         var playerRepository2 = _repositoryFactory.GetPlayerRepository();
         await playerRepository2.UpdateAllPlayerRankingsAsync();
         
         await _rankingCacheService.RefreshCacheAsync();
         await _rankingCacheService.RefreshAllTrackedPlayersAsync();

         _logger.LogInformation("PDL processing for all players completed.");
      }
      
      private async Task<Player> ProcessPlayerWithThrottlingAndGetUpdatedAsync(Player player)
      {
         await _throttler.WaitAsync();
         try
         {
            bool processed = await ProcessPlayerWithoutUpdateAsync(player);
            return processed ? player : null;
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, $"Error processing player {player.GameName}#{player.TagLine}: {ex.Message}");
            return null;
         }
         finally
         {
            _throttler.Release();
         }
      }
      
      private async Task<bool> ProcessPlayerWithoutUpdateAsync(Player playerData)
      {
         if (!playerData.TrackingEnabled)
         {
            return false;
         }

         string puuid = playerData.Puuid;
         string lastMatchId = playerData.MatchStats.LastProcessedMatchId;

         if (_lastProcessedTimeByPuuid.TryGetValue(puuid, out DateTime lastProcessed))
         {
            if (DateTime.UtcNow - lastProcessed < _minTimeBetweenProcessing)
            {
               return false;
            }
         }

         if (DateTime.UtcNow - playerData.LastUpdate < _minTimeBetweenProcessing)
         {
            return false;
         }

         var matchIds = await _riotApiService.GetMatchHistoryPuuid(puuid, 10, "normal");
         if (matchIds == null || matchIds.Count == 0)
         {
            _logger.LogWarning($"Could not retrieve match history for player {playerData.GameName}#{playerData.TagLine}");
            return false;
         }

         var newMatchIds = GetNewMatches(matchIds, lastMatchId);
         if (newMatchIds.Count == 0)
         {
            playerData.LastUpdate = DateTime.UtcNow;
            _lastProcessedTimeByPuuid[puuid] = DateTime.UtcNow;
            return true;
         }

         newMatchIds.Reverse();
         foreach (var matchId in newMatchIds)
         {
            await ProcessMatchAsync(matchId, puuid, playerData.DateAdded);
         }

         _lastProcessedTimeByPuuid[puuid] = DateTime.UtcNow;
         return true;
      }

      private IEnumerable<List<T>> SplitIntoBatches<T>(List<T> source, int batchSize)
      {
         for (int i = 0; i < source.Count; i += batchSize)
         {
            yield return source.Skip(i).Take(batchSize).ToList();
         }
      }

      private async Task<Dictionary<string, Player>> PreloadPlayersAsync(List<string> puuids)
      {
         var result = new Dictionary<string, Player>();
         
         try
         {
            var cachedPlayers = await _rankingCacheService.GetAllTrackedPlayersAsync(1, 10000);
            
            foreach (var player in cachedPlayers)
            {
               if (puuids.Contains(player.Puuid))
               {
                  result[player.Puuid] = player;
               }
            }
            
            var missingPuuids = puuids.Except(result.Keys).ToList();
            if (missingPuuids.Any())
            {
               var playerRepository = _repositoryFactory.GetPlayerRepository();
               foreach (var puuid in missingPuuids)
               {
                  var player = await playerRepository.GetPlayerByPuuidAsync(puuid);
                  if (player != null)
                  {
                     result[puuid] = player;
                  }
               }
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Error preloading players");
         }
         
         return result;
      }

      private async Task<List<Player>> GetPlayersFromCacheByPuuidsAsync(List<string> puuids)
      {
         var result = new List<Player>();
         
         try
         {
            var cachedPlayers = await _rankingCacheService.GetAllTrackedPlayersAsync(1, 10000);
            
            var playerDict = cachedPlayers
                .Where(p => puuids.Contains(p.Puuid))
                .ToDictionary(p => p.Puuid);
            
            foreach (var puuid in puuids)
            {
                if (playerDict.TryGetValue(puuid, out Player player))
                {
                    result.Add(player);
                }
            }
            
            var missingPuuids = puuids.Except(result.Select(p => p.Puuid)).ToList();
            if (missingPuuids.Any())
            {
                var playerRepository = _repositoryFactory.GetPlayerRepository();
                var dbPlayers = await playerRepository.GetPlayersByPuuidsAsync(missingPuuids);
                result.AddRange(dbPlayers);
            }
            
            _logger.LogDebug($"Found {result.Count} of {puuids.Count} players (cache: {playerDict.Count}, db: {result.Count - playerDict.Count})");
         }
         catch (Exception ex)
         {
            _logger.LogWarning($"Error accessing players: {ex.Message}");
         }
         
         return result;
      }

      private async Task UpdatePlayerAsync(Player player)
      {
         var playerRepository = _repositoryFactory.GetPlayerRepository();
         await playerRepository.UpdatePlayerAsync(player);
         
         if (player.TrackingEnabled)
         {
            try 
            {
               await _rankingCacheService.RefreshCacheAsync();
               await _rankingCacheService.RefreshAllTrackedPlayersAsync();
            }
            catch (Exception ex)
            {
               _logger.LogWarning($"Failed to refresh cache after player update: {ex.Message}");
            }
         }
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
            _ => 1500
         };
      }

      private async Task<bool> AddPlayerAsync(string puuid, string gameName, string tagLine, bool trackingEnabled, int pdl, string region, string server)
      {
         try
         {
            var playerRepository = _repositoryFactory.GetPlayerRepository();
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

            await playerRepository.CreatePlayerAsync(player);
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

      public void Dispose()
      {
         _throttler?.Dispose();
      }
   }
}

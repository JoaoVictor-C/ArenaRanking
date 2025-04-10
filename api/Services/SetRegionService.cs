using ArenaBackend.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services;

public class SetRegionService : ISetRegionService
{
   private readonly IPlayerRepository _playerRepository;
   private readonly ILogger<PdlRecalculationService> _logger;

   public SetRegionService(
       IPlayerRepository playerRepository,
       ILogger<PdlRecalculationService> logger)
   {
       _playerRepository = playerRepository;
       _logger = logger;
   }

   public Task<bool> SetRegionAll() {
      // We'll get all ranked players, then we'll get the the recent matches. After that we'll use this dictionary to get the correct region and server, based on the first 3 letters of the game ID
      var referenceRegions = new Dictionary<string, string>
      {
         { "BR1", "americas" },
         { "NA1", "americas" },
         { "LA1", "americas" },
         { "LA2", "americas" },
         { "EUN", "europe" },
         { "EUW", "europe" },
         { "TR1", "europe" },
         { "RU", "europe" },
         { "JP1", "asia" },
         { "KR", "asia" },
         { "OC1", "sea" },
         { "PH2", "sea" },
         { "SG2", "sea" },
         { "TH2", "sea" },
         { "TW2", "sea" },
         { "VN2", "sea" },
      };

      // For now let's get all unique regions that appear on the first 3 lettes of the game ID
      var players = _playerRepository.GetAllPlayersAsync().Result;

      if (players == null)
      {
         _logger.LogWarning("No players found in the database.");
         return Task.FromResult(false);
      }

      var regions = new HashSet<string>();
      foreach (var player in players)
      {
         if (player.MatchStats?.RecentGames == null || player.MatchStats.RecentGames.Count == 0 || 
             string.IsNullOrEmpty(player.Region) || string.IsNullOrEmpty(player.Server))
         {
            // Default to BR1/americas for players without games or region/server
            player.Region = "americas";
            player.Server = "br1";
            _playerRepository.UpdatePlayerAsync(player);
            continue;
         }

         var gameId = player.MatchStats.RecentGames[0];
         if (gameId != null && gameId.Length >= 3)
         {
            var regionKey = gameId.Substring(0, 3);
            if (referenceRegions.ContainsKey(regionKey))
            {
               regions.Add(referenceRegions[regionKey]);
               player.Region = referenceRegions[regionKey];
               player.Server = regionKey.ToLower();
               _playerRepository.UpdatePlayerAsync(player);
            }
            else
            {
               _logger.LogWarning($"Region not found for game ID: {gameId}");
            }
         }
         else
         {
            _logger.LogWarning($"Invalid game ID: {gameId}");
         }
      }

      // Get all players again to see if any were updated
      var updatedPlayers = _playerRepository.GetRanking().Result;
      if (updatedPlayers == null)
      {
         _logger.LogWarning("No players found in the database after update.");
         return Task.FromResult(false);
      }
      foreach (var player in updatedPlayers)
      {
         if (string.IsNullOrEmpty(player.Region) || string.IsNullOrEmpty(player.Server))
         {
            _logger.LogWarning($"Player {player.GameName}#{player.TagLine} - Region or Server not set.");
         }
         else
         {
            Console.WriteLine($"Player {player.GameName}#{player.TagLine} - Region: {player.Region}, Server: {player.Server}");
         }
      }

      return Task.FromResult(true);
   }
}
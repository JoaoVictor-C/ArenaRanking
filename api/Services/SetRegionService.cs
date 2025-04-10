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

   public Task<bool> SetRegionAll()
   {
      var referenceRegions = new Dictionary<string, (string region, string server)>
      {
         { "BR1", ("americas", "br1") },
         { "NA1", ("americas", "na1") },
         { "LA1", ("americas", "la1") },
         { "LA2", ("americas", "la2") },
         { "EUN", ("europe", "eun1") },
         { "EUW", ("europe", "euw1") },
         { "TR1", ("europe", "tr1") },
         { "RU", ("europe", "ru") },
         { "JP1", ("asia", "jp1") },
         { "KR", ("asia", "kr") },
         { "OC1", ("sea", "oc1") },
         { "PH2", ("sea", "ph2") },
         { "SG2", ("sea", "sg2") },
         { "TH2", ("sea", "th2") },
         { "TW2", ("sea", "tw2") },
         { "VN2", ("sea", "vn2") }
      };

      var players = _playerRepository.GetAllPlayersAsync().Result;
      if (players == null)
      {
         _logger.LogWarning("No players found in the database.");
         return Task.FromResult(false);
      }

      foreach (var player in players)
      {
         var tagLine = player.TagLine?.ToUpper();
         if (string.IsNullOrEmpty(tagLine))
         {
            _logger.LogWarning($"Player {player.GameName} has no tagline.");
            continue;
         }

         if (tagLine == "EUNE")
         {
            tagLine = "EUN";
         }

         if (referenceRegions.TryGetValue(tagLine, out var regionInfo))
         {
            player.Region = regionInfo.region;
            player.Server = regionInfo.server;
            // _playerRepository.UpdatePlayerAsync(player);
            _logger.LogInformation($"Updated player {player.GameName}#{player.TagLine} - Region: {player.Region}, Server: {player.Server}");
         }
         else
         {
            _logger.LogWarning($"No matching region found for tagline: {tagLine}");
         }
      }

      return Task.FromResult(true);
   }
}
using ArenaBackend.Models;
using ArenaBackend.Repositories;

namespace ArenaBackend.Services;

public class MigrateOldSystemService : IMigrateOldSystemService
{
   private readonly IOldPlayerRepository _oldPlayerRepository;
   private readonly IPlayerRepository _playerRepository;
   private readonly IRiotApiService _riotApiService;

   public MigrateOldSystemService(IOldPlayerRepository oldPlayerRepository, IPlayerRepository playerRepository, IRiotApiService riotApiService)
   {
      _oldPlayerRepository = oldPlayerRepository;
      _playerRepository = playerRepository;
      _riotApiService = riotApiService;
   }

   public async Task<bool> MigrateOldPlayers()
   {
      var oldPlayers = await _oldPlayerRepository.GetAllPlayersAsync();

      int errors = 0;

      List<Player> newPlayers = new();

      foreach (var oldPlayer in oldPlayers)
      {
         var parts = oldPlayer.RiotId.Split("#");
         var gameName = parts[0];
         var tagLine = parts[1];

         var puuid = _riotApiService.VerifyRiotId(tagLine, gameName).Result;
         if (puuid == null)
         {
            errors++;
            continue;
         }

         var newPlayer = new Player
         {
            GameName = gameName,
            TagLine = tagLine,
            TrackingEnabled = true,
            ProfileIconId = oldPlayer.ProfileIconId,
            Puuid = puuid
         };

         newPlayers.Add(newPlayer);
      }

      await _playerRepository.CreatePlayersAsync(newPlayers);
      
      // Verify if all players were migrated successfully
      var players = await _playerRepository.GetAllPlayersAsync();
      if (players.Count() == oldPlayers.Count() - errors)
      {
         return true;
      }
      return false;
   }
}
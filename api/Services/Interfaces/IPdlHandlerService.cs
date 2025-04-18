using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;

namespace ArenaBackend.Services
{
   public interface IPdlHandlerService : IDisposable
   {
      Task<bool> ProcessPlayerPdlAsync(Player playerData);

      Task ProcessAllPlayersPdlAsync();

      Task<bool> ProcessMatchAsync(string matchId, string puuid, DateTime? dateAdded);

      int CalculatePdlChange(int playerPdl, int averagePdl, int placement, int matchesPlayed = 0);

      Task<bool> UpdatePlayerPdlAsync(string puuid, int newPdl, string lastMatchId, int win, int loss, List<Dictionary<string, string>> championsPlayed, int placement, int profileIcon, GetMatchDataModel.Info matchInfo, List<PlayerDTO> playerDTO = null);
   }
}
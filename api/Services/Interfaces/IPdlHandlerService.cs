using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;

namespace ArenaBackend.Services
{
   public interface IPdlHandlerService
   {
      Task<bool> ProcessPlayerPdlAsync(Player playerData);

      Task ProcessAllPlayersPdlAsync();

      int CalculatePdlChange(int playerPdl, int averagePdl, int placement, int matchesPlayed = 0);

      Task<bool> UpdatePlayerPdlAsync(string puuid, int newPdl, string lastMatchId, int wins, int losses, List<Dictionary<string, string>> matchParticipants, int placement, int profileIconId);
   }
}
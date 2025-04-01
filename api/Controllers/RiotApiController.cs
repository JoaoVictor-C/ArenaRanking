using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ArenaBackend.Services;
using ArenaBackend.Models;
using System.Collections.Generic;
using ArenaBackend.Repositories;

namespace ArenaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RiotApiController : ControllerBase
{
   private readonly IRiotApiService _riotApiService;
   private readonly IPlayerRepository _playerRepository;

   public RiotApiController(IRiotApiService riotApiService, IPlayerRepository playerRepository)
   {
      _riotApiService = riotApiService;
      _playerRepository = playerRepository;
   }

   [HttpGet("verify/{riotId}")]
   public async Task<ActionResult<object>> VerifyRiotId(string riotId)
   {
      try
      {
         (var ResultMessage, var Success) = await _riotApiService.ConsultarRiotApi(riotId);
         if (!Success)
         {
            return NotFound(ResultMessage);
         }

          var puuid = ResultMessage;
          return Ok(new { puuid });
      }
      catch (System.Exception)
      {
         Console.WriteLine("Erro ao consultar a API da Riot - VerifyRiotId");
         throw;
      }
   }

   [HttpGet("matches/{puuid}")]
   public async Task<ActionResult<List<string>>> GetMatchHistory(string puuid)
   {
      var matches = await _riotApiService.GetMatchHistoryPuuid(puuid, 5, "NORMAL");
      if (matches == null)
      {
         return NotFound("No matches found or error occurred");
      }
      return Ok(matches);
   }

   [HttpGet("match/{matchId}")]
   public async Task<ActionResult<object>> GetMatchDetails(string matchId)
   {
      var matchDetails = await _riotApiService.GetMatchDetails(matchId);
      if (matchDetails == null)
      {
         return NotFound("Match not found or error occurred");
      }
      return Ok(matchDetails);
   }

   [HttpGet("user/{puuid}")]
   public async Task<ActionResult<string>> GetPlayer(string puuid)
   {
      // Debug purpose
      var result = await _riotApiService.GetPlayer(puuid);
      return Ok(result);
   }
}
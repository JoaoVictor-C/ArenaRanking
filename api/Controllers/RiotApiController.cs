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
   public async Task<ActionResult<string>> VerifyRiotId(string riotId)
   {
      try
      {
         Console.WriteLine(riotId);
         var result = await _riotApiService.ConsultarRiotApi(riotId);
         return Ok(result);
      }
      catch (System.Exception)
      {
         Console.WriteLine("Erro ao consultar a API da Riot");
         throw;
      }
   }

   [HttpGet("matches/{puuid}")]
   public async Task<ActionResult<List<string>>> GetMatchHistory(string puuid)
   {
      var matches = await _riotApiService.GetMatchHistoryPuuid(puuid);
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

   [HttpPost("register")]
   public async Task<ActionResult<Player>> RegisterPlayer(string riotId)
   {
      try
      {
         string[] parts = riotId.Split('#');
         if (parts.Length != 2)
         {
            return BadRequest("Invalid Riot ID format. Use name#tagline format.");
         }

         string name = parts[0];
         string tagline = parts[1];

         string? puuid = await _riotApiService.VerifyRiotId(tagline, name);
         if (puuid == null)
         {
            return NotFound($"Player {riotId} not found in Riot API.");
         }

         // Check if player already exists
         var existingPlayers = await _playerRepository.GetAllPlayersAsync();
         var existingPlayer = existingPlayers.FirstOrDefault(p => p.Puuid == puuid);

         if (existingPlayer != null)
         {
            return BadRequest($"Player with Riot ID {riotId} is already registered.");
         }

         // Create new player
         var player = new Player
         {
            Puuid = puuid,
            RiotId = riotId,
            Nome = name,
            DateAdded = System.DateTime.UtcNow
         };

         await _playerRepository.CreatePlayerAsync(player);
         return CreatedAtAction(nameof(RegisterPlayer), new { riotId }, player);
      }
      catch (System.Exception ex)
      {
         return StatusCode(500, $"An error occurred: {ex.Message}");
      }
   }
}
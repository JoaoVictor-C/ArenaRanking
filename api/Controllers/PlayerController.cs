using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;
using ArenaBackend.Repositories;
using ArenaBackend.Services;

namespace ArenaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayerController : ControllerBase
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IRiotApiService _riotApiService;
    private readonly IPdlHandlerService _pdlHandlerService;

    public PlayerController(IPlayerRepository playerRepository, IRiotApiService riotApiService, IPdlHandlerService pdlHandlerService)
    {
        _pdlHandlerService = pdlHandlerService;
        _playerRepository = playerRepository;
        _riotApiService = riotApiService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Player>>> GetAllPlayers()
    {
        var players = await _playerRepository.GetAllPlayersAsync();
        return Ok(players);
    }

    [HttpGet("ranking")]
    public async Task<ActionResult<IEnumerable<Player>>> GetRanking()
    {
        var players = await _playerRepository.GetAllPlayersAsync();
        // Show only players with at least 1 match played
        players = players.Where(p => p.MatchStats.TotalGames > 0 && p.TrackingEnabled == true).ToList();
        var ranking = players.OrderByDescending(p => p.Pdl).ToList();
        return Ok(ranking);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Player>>> SearchPlayers()
    {
        // Similar to ranking but return some of data
        var players = await _playerRepository.GetAllPlayersAsync();
        players = players.Where(p => p.TrackingEnabled == true).ToList();
        var ranking = players.OrderByDescending(p => p.Pdl).ToList();
        return Ok(ranking.Select(p => new { p.GameName, p.TagLine, p.ProfileIconId  }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Player>> GetPlayer(string id)
    {
        var player = await _playerRepository.GetPlayerByIdAsync(id);
        if (player == null)
        {
            return NotFound();
        }
        return Ok(player);
    }

    [HttpGet("riot")]
    public async Task<ActionResult<Player>> GetPlayerByRiotId([FromQuery] string gameName, [FromQuery] string tagLine)
    {
        var player = await _playerRepository.GetPlayerByRiotIdAsync(gameName, tagLine);
        if (player == null)
        {
            return NotFound();
        }
        return Ok(player);
    }

    [HttpPost]
    public async Task<ActionResult<Player>> CreatePlayer(Player player)
    {
        player.DateAdded = System.DateTime.UtcNow;
        await _playerRepository.CreatePlayerAsync(player);
        return CreatedAtAction(nameof(GetPlayer), new { id = player.Id }, player);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlayer(string id, Player player)
    {
        if (id != player.Id)
        {
            return BadRequest();
        }

        var existingPlayer = await _playerRepository.GetPlayerByIdAsync(id);
        if (existingPlayer == null)
        {
            return NotFound();
        }

        await _playerRepository.UpdatePlayerAsync(player);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlayer(string id)
    {
        var player = await _playerRepository.GetPlayerByIdAsync(id);
        if (player == null)
        {
            return NotFound();
        }

        await _playerRepository.DeletePlayerAsync(id);
        return NoContent();
    }

    [HttpPut("rename")]
    public async Task<IActionResult> RenamePlayer([FromQuery] string gameName, [FromQuery] string tagLine, [FromQuery] string newName)
    {
        var player = await _playerRepository.GetPlayerByRiotIdAsync(gameName, tagLine);
        if (player == null)
        {
            return NotFound();
        }

        player.GameName = newName;
        await _playerRepository.UpdatePlayerAsync(player);
        return NoContent();
    }
    
    [HttpGet("processPlayer")]
    public async Task<IActionResult> ProcessPlayer([FromQuery] string gameName, [FromQuery] string tagLine)
    {
        var player = await _playerRepository.GetPlayerByRiotIdAsync(gameName, tagLine);
        if (player == null)
        {
            return NotFound();
        }

        // Process the player data
        await _pdlHandlerService.ProcessPlayerPdlAsync(player);

        return Ok(player);
    }


    [HttpPost("register")]
    public async Task<ActionResult<Player>> RegisterPlayer([FromQuery] string tagLine, [FromQuery] string gameName)
    {
        try
        {
            string? puuid = await _riotApiService.VerifyRiotId(tagLine, gameName);
            if (puuid == null)
            {
                return NotFound($"Player {gameName} not found in Riot API.");
            }

            // Check if player already exists
            var existingPlayer = await _playerRepository.GetPlayerByPuuidAsync(puuid);

            if (existingPlayer != null)
            {
                if (existingPlayer.TrackingEnabled)
                {
                    return BadRequest($"Player {gameName} is already registered and being tracked.");
                }
                else
                {
                    // Player exists but tracking is disabled, reset data and enable tracking
                    existingPlayer.TrackingEnabled = true;
                    existingPlayer.Pdl = 1000;
                    existingPlayer.MatchStats = new MatchStats();
                    existingPlayer.LastUpdate = System.DateTime.UtcNow;
                    existingPlayer.DateAdded = System.DateTime.UtcNow;

                    await _playerRepository.UpdatePlayerAsync(existingPlayer);
                    return Ok(existingPlayer);
                }
            }

            // Create new player
            var player = new Player
            {
                Puuid = puuid,
                GameName = gameName,
                TagLine = tagLine,
                DateAdded = System.DateTime.UtcNow,
                LastUpdate = System.DateTime.UtcNow,
                TrackingEnabled = true,
            };

            await _playerRepository.CreatePlayerAsync(player);
            return CreatedAtAction(nameof(RegisterPlayer), new { puuid }, player);
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

}
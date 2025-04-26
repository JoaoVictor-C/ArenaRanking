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
    private readonly IPdlRecalculationService _pdlRecalculationService;
    private readonly IRankingCacheService _rankingCacheService;
    private readonly ISetRegionService _setRegionService;

    public PlayerController(
        IPlayerRepository playerRepository,
        IRiotApiService riotApiService,
        IPdlHandlerService pdlHandlerService,
        IPdlRecalculationService pdlRecalculationService,
        IRankingCacheService rankingCacheService,
        ISetRegionService setRegionService)
    {
        _pdlHandlerService = pdlHandlerService;
        _playerRepository = playerRepository;
        _riotApiService = riotApiService;
        _pdlRecalculationService = pdlRecalculationService;
        _rankingCacheService = rankingCacheService;
        _setRegionService = setRegionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Player>>> GetAllPlayers()
    {
        var players = await _playerRepository.GetAllPlayersAsync();
        return Ok(players);
    }

    [HttpGet("ranking")]
    public async Task<ActionResult<IEnumerable<Player>>> GetRanking([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var players = await _rankingCacheService.GetCachedRankingAsync(page, pageSize);

        foreach (var player in players)
        {
            if (player.MatchStats?.RecentGames != null)
            {
                player.MatchStats.RecentGames = player.MatchStats.RecentGames
                    .Select(g => new RecentGame
                    {
                        MatchId = g.MatchId,
                        GameCreation = g.GameCreation,
                        GameDuration = g.GameDuration,
                        Players = [] // ← zeramos os Players para ranking ficar leve!
                    })
                    .ToList();
            }
        }

        return Ok(players);
    }

    [HttpGet("ranking/total")]
    public async Task<ActionResult<int>> GetTotalPlayers()
    {
        var totalPlayers = await _rankingCacheService.GetTotalPlayersAsync();
        return Ok(totalPlayers);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Player>>> SearchPlayers([FromQuery] string? gameName, [FromQuery] string? tagLine, [FromQuery] int? limit, [FromQuery] string server)
    {
        if (string.IsNullOrEmpty(gameName) && string.IsNullOrEmpty(tagLine))
        {
            return BadRequest("At least one search parameter (gameName, tagLine) must be provided");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be greater than 0");
        }

        if (!string.IsNullOrEmpty(server) && !new[] { "br1", "na1", "euw1", "kr", "jp1", "eun1", "tr1", "la1", "la2", "oc1", "ph2", "sg2", "th2", "tw2", "vn2" }.Contains(server.ToLower()))
        {
            return BadRequest("Invalid server region specified");
        }

        var players = await _rankingCacheService.GetCachedRankingAsync(1, 99999);
        if (!string.IsNullOrEmpty(gameName))
        {
            players = players.Where(p => p.GameName.Contains(gameName, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrEmpty(tagLine))
        {
            players = players.Where(p => p.TagLine.Contains(tagLine, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrEmpty(server))
        {
            players = players.Where(p => p.Server.Equals(server, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (limit.HasValue)
        {
            players = players.Take(limit.Value).ToList();
        }

        // Create a mask to return only the GameName, TagLine, ProfileIconId
        var maskedPlayers = players.Select(p => new Player
        {
            GameName = p.GameName,
            TagLine = p.TagLine,
            ProfileIconId = p.ProfileIconId,
            Server = p.Server,
            Pdl = p.Pdl,
            LastUpdate = p.LastUpdate,
            DateAdded = p.DateAdded
        }).ToList();

        return Ok(maskedPlayers);
    }

    [HttpGet("get/player")]
    public async Task<ActionResult<IEnumerable<Player>>> GetPlayer([FromQuery] string? gameName, [FromQuery] string? tagLine, [FromQuery] string server)
    {
        if (string.IsNullOrEmpty(gameName) && string.IsNullOrEmpty(tagLine))
        {
            return BadRequest("At least one search parameter (gameName, tagLine) must be provided");
        }

        if (!string.IsNullOrEmpty(server) && !new[] { "br1", "na1", "euw1", "kr", "jp1", "eun1", "tr1", "la1", "la2", "oc1", "ph2", "sg2", "th2", "tw2", "vn2" }.Contains(server.ToLower()))
        {
            return BadRequest("Invalid server region specified");
        }

        var players = await _rankingCacheService.GetCachedRankingAsync(1, 99999);
        if (!string.IsNullOrEmpty(gameName))
        {
            players = players.Where(p => p.GameName.Contains(gameName, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrEmpty(tagLine))
        {
            players = players.Where(p => p.TagLine.Contains(tagLine, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrEmpty(server))
        {
            players = players.Where(p => p.Server.Equals(server, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        players = players.Take(1).ToList();

        return Ok(players);
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
            string riotId = $"{gameName}#{tagLine}";
            // Verify if the player exists in the Riot API using the ConsultarRiotApi
            var (puuid, success) = await _riotApiService.ConsultarRiotApi(riotId);
            if (!success)
            {
                return BadRequest($"Player {gameName}#{tagLine} not found in Riot API.");
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

    [HttpPost("recalculate-all")]
    public async Task<IActionResult> RecalculateAllPlayers()
    {
        await _pdlRecalculationService.RecalculateAllPlayersPdlAsync();
        return Ok("Recálculo de PDL para todos os jogadores foi iniciado.");
    }

    [HttpPost("recalculate-player")]
    public async Task<IActionResult> RecalculatePlayer([FromQuery] string puuid)
    {
        await _pdlRecalculationService.RecalculatePlayerPdlAsync(puuid);
        return Ok($"Recálculo de PDL para o jogador com PUUID {puuid} foi concluído.");
    }

    [HttpPost("recalculate-by-riot-id")]
    public async Task<IActionResult> RecalculatePlayerByRiotId([FromQuery] string gameName, [FromQuery] string tagLine)
    {
        var player = await _playerRepository.GetPlayerByRiotIdAsync(gameName, tagLine);
        if (player == null)
        {
            return NotFound($"Jogador {gameName}#{tagLine} não encontrado.");
        }

        await _pdlRecalculationService.RecalculatePlayerPdlAsync(player.Puuid);
        return Ok($"Recálculo de PDL para o jogador {gameName}#{tagLine} foi concluído.");
    }

    // Test

    [HttpGet("SetRegionAll")]
    public async Task<IActionResult> SetRegionAll()
    {
        var result = await _setRegionService.SetRegionAll();
        if (result)
        {
            return Ok("Regiões definidas com sucesso.");
        }
        else
        {
            return BadRequest("Falha ao definir regiões.");
        }
    }
}
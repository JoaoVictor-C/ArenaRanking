using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;
using ArenaBackend.Repositories;

namespace ArenaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayerController : ControllerBase
{
    private readonly IPlayerRepository _playerRepository;

    public PlayerController(IPlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Player>>> GetAllPlayers()
    {
        var players = await _playerRepository.GetAllPlayersAsync();
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
}
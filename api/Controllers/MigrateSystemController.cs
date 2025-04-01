using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArenaBackend.Models;
using ArenaBackend.Repositories;
using ArenaBackend.Services;

namespace ArenaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MigrateSystemController : ControllerBase
{
    private readonly IMigrateOldSystemService _migrateOldSystemService;

      public MigrateSystemController(IMigrateOldSystemService migrateOldSystemService)
      {
         _migrateOldSystemService = migrateOldSystemService;
      }

      [HttpGet("migrate")]
      public async Task<ActionResult<string>> MigrateOldSystem()
      {
         var result = await _migrateOldSystemService.MigrateOldPlayers();
         return Ok(result);
      }
}
using Microsoft.AspNetCore.Mvc;
using ArenaBackend.Services;
using Microsoft.Extensions.Logging;
using ArenaBackend.Models;
using ArenaBackend.Configs;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace ArenaBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApiKeyController : ControllerBase
    {
        private readonly IRiotApiKeyManager _apiKeyManager;
        private readonly ILogger<ApiKeyController> _logger;

        public ApiKeyController(IRiotApiKeyManager apiKeyManager, ILogger<ApiKeyController> logger)
        {
            _apiKeyManager = apiKeyManager;
            _logger = logger;
        }

        [HttpPost("update")]
        public IActionResult UpdateApiKey([FromBody] ApiKeyUpdateRequest request)
        {
            if (string.IsNullOrEmpty(request.ApiKey))
            {
                return BadRequest("A chave da API não pode estar vazia");
            }

            _apiKeyManager.UpdateApiKey(request.ApiKey);
            _logger.LogInformation("Chave da API da Riot atualizada com sucesso");
            return Ok(new { message = "Chave da API atualizada com sucesso" });
        }
    }

    public class ApiKeyUpdateRequest
    {
        public string ApiKey { get; set; }
    }
}
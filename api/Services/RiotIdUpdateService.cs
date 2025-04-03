using ArenaBackend.Models;
using ArenaBackend.Repositories;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services
{
    public class RiotIdUpdateService : IRiotIdUpdateService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IRiotApiService _riotApiService;
        private readonly ILogger<RiotIdUpdateService> _logger;

        public RiotIdUpdateService(
            IPlayerRepository playerRepository,
            IRiotApiService riotApiService,
            ILogger<RiotIdUpdateService> logger)
        {
            _playerRepository = playerRepository;
            _riotApiService = riotApiService;
            _logger = logger;
        }

        public async Task UpdateAllPlayersRiotIdsAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando atualização diária de Riot IDs");

                var allPlayers = await _playerRepository.GetAllPlayersAsync();
                // For now we'll get only the player Presente#1001 for debugging purposes
                // var player = await _playerRepository.GetPlayerByRiotIdAsync("Presente", "1001");
                int updatedCount = 0;

                foreach (var player in allPlayers)
                {
                    try
                    {
                        // Obtém informações atualizadas do jogador usando a API da Riot
                        GetRiotIdDataModel riotIdInfo = await _riotApiService.GetRiotIdByPuuid(player.Puuid);
                        if (riotIdInfo == null)
                        {
                            _logger.LogWarning($"Riot ID não encontrado para o jogador {player.GameName}#{player.TagLine} (PUUID: {player.Puuid})");
                            return;
                        }

                        if (riotIdInfo != null &&
                            (!string.Equals(player.GameName, riotIdInfo.GameName) ||
                             !string.Equals(player.TagLine, riotIdInfo.TagLine)))
                        {
                            // Registra os valores anteriores para o log
                            string oldGameName = player.GameName;
                            string oldTagLine = player.TagLine;

                            // Atualiza o jogador se o nome ou tagLine mudou
                            player.GameName = riotIdInfo.GameName;
                            player.TagLine = riotIdInfo.TagLine;
                            //await _playerRepository.UpdatePlayerAsync(player);
                            updatedCount++;

                            _logger.LogInformation($"Riot ID atualizado de {oldGameName}#{oldTagLine} para {player.GameName}#{player.TagLine} (PUUID: {player.Puuid})");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Erro ao atualizar Riot ID para jogador {player.GameName}#{player.TagLine} (PUUID: {player.Puuid})");
                    }
                }

                _logger.LogInformation($"Atualização de Riot IDs concluída. {updatedCount} jogadores atualizados.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante atualização dos Riot IDs");
            }
        }
    }
}
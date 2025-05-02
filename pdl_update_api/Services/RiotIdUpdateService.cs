using ArenaBackend.Models;
using ArenaBackend.Repositories;
using ArenaBackend.Factories;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services
{
    public class RiotIdUpdateService : IRiotIdUpdateService
    {
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IRiotApiService _riotApiService;
        private readonly ILogger<RiotIdUpdateService> _logger;

        public RiotIdUpdateService(
            IRepositoryFactory repositoryFactory,
            IRiotApiService riotApiService,
            ILogger<RiotIdUpdateService> logger)
        {
            _repositoryFactory = repositoryFactory;
            _riotApiService = riotApiService;
            _logger = logger;
        }

        public async Task UpdateAllPlayersRiotIdsAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando atualização diária de Riot IDs");

                var playerRepository = _repositoryFactory.GetPlayerRepository();
                var allPlayers = await playerRepository.GetAllPlayersAsync();
                
                if (allPlayers.Count() == 0)
                {
                    _logger.LogInformation("Nenhum jogador encontrado para atualizar os Riot IDs.");
                    return;
                }

                int updatedCount = 0;

                foreach (var player in allPlayers)
                {
                    try
                    {
                        GetRiotIdDataModel? riotIdInfo = await _riotApiService.GetRiotIdByPuuid(player.Puuid);
                        if (riotIdInfo == null)
                        {
                            _logger.LogWarning($"Riot ID não encontrado para o jogador {player.GameName}#{player.TagLine} (PUUID: {player.Puuid})");
                            return;
                        }

                        if (riotIdInfo != null &&
                            (!string.Equals(player.GameName, riotIdInfo.GameName) ||
                             !string.Equals(player.TagLine, riotIdInfo.TagLine)))
                        {
                            string oldGameName = player.GameName;
                            string oldTagLine = player.TagLine;

                            player.GameName = riotIdInfo.GameName;
                            player.TagLine = riotIdInfo.TagLine;
                            await playerRepository.UpdatePlayerAsync(player);
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
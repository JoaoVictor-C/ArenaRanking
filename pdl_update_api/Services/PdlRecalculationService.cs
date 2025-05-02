using ArenaBackend.Models;
using ArenaBackend.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArenaBackend.Services
{
    public class PdlRecalculationService : IPdlRecalculationService
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IRiotApiService _riotApiService;
        private readonly IPdlHandlerService _pdlHandlerService;
        private readonly ILogger<PdlRecalculationService> _logger;

        public PdlRecalculationService(
            IPlayerRepository playerRepository,
            IRiotApiService riotApiService,
            IPdlHandlerService pdlHandlerService,
            ILogger<PdlRecalculationService> logger)
        {
            _playerRepository = playerRepository;
            _riotApiService = riotApiService;
            _pdlHandlerService = pdlHandlerService;
            _logger = logger;
        }

        public async Task RecalculateAllPlayersPdlAsync()
        {
            _logger.LogInformation("Iniciando recálculo de PDL para todos os jogadores...");

            var allPlayers = await _playerRepository.GetAllPlayersAsync();
            var trackingPlayers = allPlayers.Where(p => p.TrackingEnabled).ToList();

            _logger.LogInformation($"Total de {trackingPlayers.Count} jogadores com rastreamento ativo para recalcular");

            foreach (var player in trackingPlayers)
            {
                await RecalculatePlayerPdlAsync(player.Puuid);
                await Task.Delay(500);
            }

            await _playerRepository.UpdateAllPlayerRankingsAsync();

            _logger.LogInformation("Recálculo de PDL para todos os jogadores concluído.");
        }

        public async Task RecalculatePlayerPdlAsync(string puuid)
        {
            var player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
            if (player == null || !player.TrackingEnabled)
            {
                _logger.LogWarning($"Jogador com PUUID {puuid} não encontrado ou rastreamento desativado.");
                return;
            }

            _logger.LogInformation($"Recalculando PDL para o jogador {player.GameName}#{player.TagLine}...");

            DateTime? originalDateAdded = player.DateAdded;
            string gameName = player.GameName;
            string tagLine = player.TagLine;
            int profileIconId = player.ProfileIconId;
            int matchesToProcess = player.MatchStats.Win + player.MatchStats.Loss;

            var matchIds = await _riotApiService.GetMatchHistoryPuuid(puuid, matchesToProcess, "NORMAL");
            if (matchIds == null || matchIds.Count == 0)
            {
                _logger.LogWarning($"Nenhuma partida encontrada para o jogador {player.GameName}#{player.TagLine}");
                return;
            }

            await ResetPlayerDataAsync(player);

            matchIds.Reverse();
            int processedCount = 0;

            foreach (var matchId in matchIds)
            {
                try
                {
                    var matchDetails = await _riotApiService.GetMatchDetails(matchId);
                    if (matchDetails == null || matchDetails.info.gameMode != "CHERRY")
                    {
                        continue;
                    }

                    bool participated = matchDetails.info.participants.Any(p => p.puuid == puuid);
                    if (!participated)
                    {
                        continue;
                    }

                    player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
                    player.MatchStats.LastProcessedMatchId = "";

                    await _pdlHandlerService.ProcessMatchAsync(matchId, player.Puuid, player.DateAdded);

                    processedCount++;

                    _logger.LogInformation($"Processada partida {matchId} para {player.GameName}#{player.TagLine} ({processedCount}/{matchIds.Count})");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erro ao processar partida {matchId} para jogador {puuid}");
                }
            }

            player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
            _logger.LogInformation($"Recálculo de PDL concluído para {player.GameName}#{player.TagLine}. PDL atualizado: {player.Pdl}, Partidas processadas: {processedCount}. Vitórias: {player.MatchStats.Win}, Derrotas: {player.MatchStats.Loss}");
        }

        private async Task ResetPlayerDataAsync(Player player)
        {
            string id = player.Id;
            string puuid = player.Puuid;
            string gameName = player.GameName;
            string tagLine = player.TagLine;
            int profileIconId = player.ProfileIconId;
            DateTime? dateAdded = player.DateAdded;
            bool trackingEnabled = player.TrackingEnabled;

            player.Pdl = 1000;
            player.MatchStats = new MatchStats();
            player.LastPlacement = 0;
            player.RankPosition = 0;
            player.LastUpdate = DateTime.UtcNow;

            await _playerRepository.UpdatePlayerAsync(player);
            _logger.LogInformation($"Dados resetados para o jogador {player.GameName}#{player.TagLine}");
        }
    }
}
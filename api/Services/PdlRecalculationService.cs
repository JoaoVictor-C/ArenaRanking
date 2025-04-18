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

            _logger.LogInformation($"Total de {allPlayers.Count()} jogadores com rastreamento ativo para recalcular");

            foreach (var player in allPlayers)
            {
                await ResetPuuid(player);
            }

            // Get the players again
            allPlayers = await _playerRepository.GetAllPlayersAsync();
            // After resetting PUUIDs, let's check each one to se if they are valid, by calling the method GetTier if it doesn't return a string send an error
            foreach (var player in allPlayers)
            {
                var riotId = await _riotApiService.GetRiotIdByPuuid(player.Puuid, player.Region);
                if (riotId == null)
                {
                    _logger.LogWarning("Riot ID não encontrado para o jogador {GameName}#{TagLine}", 
                        player.GameName, player.TagLine);
                    await ResetPuuid(player);
                }
            }

            _logger.LogInformation("Recálculo de PDL para todos os jogadores concluído.");
        }

        private async Task ResetPuuid(Player player)
        {
            var riotId = await _riotApiService.GetPuuidByRiotId(player.GameName, player.TagLine, player.Region);
            var puuid = riotId?.Puuid;
            if (puuid == null)
            {
                _logger.LogWarning($"PUUID não encontrado para o jogador {player.GameName}#{player.TagLine}");
                return;
            }
            if (player.Puuid == puuid)
            {
                _logger.LogInformation($"PUUID já está correto para o jogador {player.GameName}#{player.TagLine}");
                return;
            }

            player.Puuid = puuid;

            await _playerRepository.UpdatePlayerAsync(player);
            _logger.LogInformation($"PUUID atualizado para o jogador {player.GameName}#{player.TagLine}: {puuid}");
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

            // Armazenar dados que precisamos preservar
            string id = player.Id;
            string playerPuuid = player.Puuid;
            string gameName = player.GameName;
            string tagLine = player.TagLine;
            int profileIconId = player.ProfileIconId;
            string region = player.Region;
            string server = player.Server;
            DateTime? dateAdded = player.DateAdded;
            bool trackingEnabled = player.TrackingEnabled;
            int matchesToProcess = player.MatchStats.Win + player.MatchStats.Loss;

            // Obter histórico de partidas
            var matchIds = await _riotApiService.GetMatchHistoryPuuid(puuid, matchesToProcess, "NORMAL");
            if (matchIds == null || matchIds.Count == 0)
            {
                _logger.LogWarning($"Nenhuma partida encontrada para o jogador {player.GameName}#{player.TagLine}");
                return;
            }

            // Resetar dados do jogador mantendo informações importantes
            player = await ResetPlayerDataAsync(player);

            // Processar cada partida cronologicamente (da mais antiga para a mais recente)
            matchIds.Reverse(); // API da Riot retorna as mais recentes primeiro
            int processedCount = 0;

            foreach (var matchId in matchIds)
            {
                try
                {
                    var matchDetails = await _riotApiService.GetMatchDetails(matchId);
                    if (matchDetails == null || matchDetails.info.gameMode != "CHERRY")
                    {
                        continue; // Ignorar partidas que não são do modo arena
                    }

                    // Verificar se o jogador participou desta partida
                    bool participated = matchDetails.info.participants.Any(p => p.puuid == puuid);
                    if (!participated)
                    {
                        continue;
                    }

                    // Obter o jogador atualizado antes de processar a próxima partida
                    player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
                    player.MatchStats.LastProcessedMatchId = ""; // Resetar para forçar processamento
                    await _playerRepository.UpdatePlayerAsync(player); // Atualizar o jogador antes do processamento

                    // Processar a partida usando o serviço existente
                    await _pdlHandlerService.ProcessMatchAsync(matchId, player.Puuid, player.DateAdded);

                    processedCount++;

                    _logger.LogInformation($"Processada partida {matchId} para {player.GameName}#{player.TagLine} ({processedCount}/{matchIds.Count})");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erro ao processar partida {matchId} para jogador {puuid}");
                }
            }

            // Recuperar o jogador atualizado
            player = await _playerRepository.GetPlayerByPuuidAsync(puuid);
            _logger.LogInformation($"Recálculo de PDL concluído para {player.GameName}#{player.TagLine}. PDL atualizado: {player.Pdl}, Partidas processadas: {processedCount}. Vitórias: {player.MatchStats.Win}, Derrotas: {player.MatchStats.Loss}");
        }

        private async Task<Player> ResetPlayerDataAsync(Player player)
        {
            // Preservar dados de identificação e tracking
            string id = player.Id;
            string puuid = player.Puuid;
            string gameName = player.GameName;
            string tagLine = player.TagLine;
            int profileIconId = player.ProfileIconId;
            DateTime? dateAdded = player.DateAdded;
            bool trackingEnabled = player.TrackingEnabled;
            string region = player.Region;
            string server = player.Server;

            // Resetar estatísticas
            player.Pdl = 1000; // PDL inicial padrão
            player.MatchStats = new MatchStats();
            player.LastPlacement = 0;
            player.RankPosition = 0;
            player.LastUpdate = DateTime.UtcNow;

            // Garantir que os dados de identificação sejam mantidos
            player.Id = id;
            player.Puuid = puuid;
            player.GameName = gameName;
            player.TagLine = tagLine;
            player.ProfileIconId = profileIconId;
            player.DateAdded = dateAdded;
            player.TrackingEnabled = trackingEnabled;
            player.Region = region;
            player.Server = server;

            // Atualizar o jogador no banco de dados
            await _playerRepository.UpdatePlayerAsync(player);
            _logger.LogInformation($"Dados resetados para o jogador {player.GameName}#{player.TagLine}");
            
            return player;
        }
    }
}
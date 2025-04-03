using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArenaBackend.Services
{
    public class RiotIdUpdateHostedService : BackgroundService
    {
        private readonly ILogger<RiotIdUpdateHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        // TimeZone para Brasília
        private static readonly TimeZoneInfo BrasiliaTimeZone = GetBrasiliaTimeZone();
        
        public RiotIdUpdateHostedService(
            ILogger<RiotIdUpdateHostedService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de atualização de Riot IDs iniciado");
            
            // Pequeno atraso para permitir que a aplicação inicialize completamente
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Calcula o tempo até a próxima execução (4h da manhã em horário de Brasília)
                var delay = CalculateDelayUntil4AM();
                
                _logger.LogInformation(
                    "Próxima atualização de Riot IDs agendada para {NextRun} (em {Delay})",
                    DateTime.UtcNow.Add(delay).ToString("yyyy-MM-dd HH:mm:ss"),
                    delay);
                
                // Espera até a próxima execução
                await Task.Delay(delay, stoppingToken);
                
                if (!stoppingToken.IsCancellationRequested)
                {
                    // Executa a atualização dos IDs
                    await UpdateRiotIdsAsync(stoppingToken);
                }
            }
        }

        private TimeSpan CalculateDelayUntil4AM()
        {
            // Obtém a hora atual em Brasília
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrasiliaTimeZone);
            
            // Define o próximo horário de execução às 4h
            DateTime target = now.Date.AddHours(4);
            
            // Se já passou das 4h hoje, agenda para amanhã
            if (now >= target)
            {
                target = target.AddDays(1);
            }
            
            // Converte de volta para UTC para calcular o delay
            DateTime targetUtc = TimeZoneInfo.ConvertTimeToUtc(target, BrasiliaTimeZone);
            return targetUtc - DateTime.UtcNow;
        }

        private async Task UpdateRiotIdsAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Iniciando processo agendado de atualização de Riot IDs");
                
                using var scope = _serviceProvider.CreateScope();
                var updateService = scope.ServiceProvider.GetRequiredService<IRiotIdUpdateService>();
                await updateService.UpdateAllPlayersRiotIdsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante o processo agendado de atualização de Riot IDs");
            }
        }
        
        private static TimeZoneInfo GetBrasiliaTimeZone()
        {
            try
            {
                // Linux usa o formato IANA
                return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }
            catch
            {
                try
                {
                    // Windows usa formato Windows
                    return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                }
                catch
                {
                    // Fallback: usar UTC-3 diretamente
                    return TimeZoneInfo.CreateCustomTimeZone(
                        "Brasilia Standard Time",
                        new TimeSpan(-3, 0, 0),
                        "Brasilia Standard Time",
                        "Brasilia Standard Time");
                }
            }
        }
    }
}
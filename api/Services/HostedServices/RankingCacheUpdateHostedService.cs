using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArenaBackend.Services
{
    public class RankingCacheUpdateHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RankingCacheUpdateHostedService> _logger;
        private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(1);

        public RankingCacheUpdateHostedService(
            IServiceProvider serviceProvider,
            ILogger<RankingCacheUpdateHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de atualização de cache de ranking iniciado.");

            // Atualize o cache imediatamente na inicialização
            await UpdateCacheAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_updateInterval, stoppingToken);
                    await UpdateCacheAsync();
                }
                catch (OperationCanceledException)
                {
                    // Operação cancelada normalmente na parada do aplicativo
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro durante a atualização do cache");
                }
            }

            _logger.LogInformation("Serviço de atualização de cache de ranking encerrado.");
        }

        private async Task UpdateCacheAsync()
        {
            _logger.LogInformation("Executando atualização programada do cache de ranking...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var cacheService = scope.ServiceProvider.GetRequiredService<IRankingCacheService>();
                await cacheService.RefreshCacheAsync();
            }
        }
    }
}
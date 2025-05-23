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
        private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(3);
        private int _consecutiveErrors = 0;
        private const int _maxConsecutiveErrors = 3;
        private bool _isProcessing = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

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

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            _ = ProcessCacheUpdateAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_isProcessing)
                {
                    _ = ProcessCacheUpdateAsync(stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Ignorando atualização do cache de ranking pois já existe um processamento em andamento");
                }

                await Task.Delay(_updateInterval, stoppingToken);
            }

            _logger.LogInformation("Serviço de atualização de cache de ranking encerrado.");
        }

        private async Task ProcessCacheUpdateAsync(CancellationToken stoppingToken)
        {
            if (!await _semaphore.WaitAsync(0, stoppingToken))
            {
                return;
            }

            try
            {
                _isProcessing = true;
                _logger.LogInformation("Executando atualização programada do cache de ranking...");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var cacheService = scope.ServiceProvider.GetRequiredService<IRankingCacheService>();
                    await cacheService.RefreshCacheAsync();
                }

                _logger.LogInformation("Atualização do cache de ranking finalizada");

                _consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _logger.LogError(ex, "Erro durante a atualização do cache. Erro consecutivo: {Count}/{Max}", 
                    _consecutiveErrors, _maxConsecutiveErrors);

                if (_consecutiveErrors >= _maxConsecutiveErrors)
                {
                    _logger.LogCritical("Número máximo de erros consecutivos atingido. Pausando o serviço por um período prolongado.");
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                    _consecutiveErrors = 0;
                }
            }
            finally
            {
                _isProcessing = false;
                _semaphore.Release();
            }
        }

        public override void Dispose()
        {
            _semaphore.Dispose();
            base.Dispose();
        }
    }
}
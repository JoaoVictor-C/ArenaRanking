using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ArenaBackend.Services
{
    public class PdlUpdateHostedService : BackgroundService
    {
        private readonly ILogger<PdlUpdateHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private int _consecutiveErrors = 0;
        private const int _maxConsecutiveErrors = 3;
        private bool _isProcessing = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public PdlUpdateHostedService(
            ILogger<PdlUpdateHostedService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PDL Update Background Service starting");

            // Small delay to let the application start completely
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Check if PDL update is already running
                if (!_isProcessing)
                {
                    // Start PDL processing on a separate thread
                    _ = ProcessPdlUpdateAsync(stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Ignorando verificação de PDL pois já existe um processamento em andamento");
                }

                // Wait for 2 minutes before next execution
                await Task.Delay(TimeSpan.FromMinutes(4), stoppingToken);
            }
        }

        private async Task ProcessPdlUpdateAsync(CancellationToken stoppingToken)
        {
            // Try to enter the semaphore (atomic check and set)
            if (!await _semaphore.WaitAsync(0, stoppingToken))
            {
                // If we couldn't acquire the semaphore immediately, it means another thread is processing
                return;
            }

            try
            {
                _isProcessing = true;
                _logger.LogInformation("Executando verificação periódica de PDL");
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var pdlService = scope.ServiceProvider.GetRequiredService<IPdlHandlerService>();
                    await pdlService.ProcessAllPlayersPdlAsync();
                }
                
                _logger.LogInformation("Verificação periódica de PDL finalizada");

                // Resetar contador de erros após sucesso
                _consecutiveErrors = 0;
            }
            catch (KeyNotFoundException ex)
            {
                _consecutiveErrors++;
                _logger.LogError(ex, "Erro de chave não encontrada durante o processamento de PDL. Erro consecutivo: {Count}/{Max}",
                    _consecutiveErrors, _maxConsecutiveErrors);

                if (_consecutiveErrors >= _maxConsecutiveErrors)
                {
                    _logger.LogCritical("Número máximo de erros consecutivos atingido. Pausando o serviço por um período prolongado.");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    _consecutiveErrors = 0;
                }
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _logger.LogError(ex, "Erro ocorreu durante processamento de atualizações PDL");

                if (_consecutiveErrors >= _maxConsecutiveErrors)
                {
                    _logger.LogCritical("Número máximo de erros consecutivos atingido. Pausando o serviço por um período prolongado.");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
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
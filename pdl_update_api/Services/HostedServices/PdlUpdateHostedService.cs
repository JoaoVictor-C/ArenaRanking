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

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_isProcessing)
                {
                    _ = ProcessPdlUpdateAsync(stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Ignorando verificação de PDL pois já existe um processamento em andamento");
                }

                await Task.Delay(TimeSpan.FromMinutes(6), stoppingToken);
            }
        }

        private async Task ProcessPdlUpdateAsync(CancellationToken stoppingToken)
        {
            if (!await _semaphore.WaitAsync(0, stoppingToken))
            {
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sgcf.Infrastructure.Bcb;

namespace Sgcf.Jobs.Jobs;

internal sealed partial class IngestaoPtaxJob(
    IServiceScopeFactory scopeFactory,
    ILogger<IngestaoPtaxJob> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Ingestão PTAX iniciada.")]
    private static partial void LogIniciada(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro na ingestão PTAX: {Message}")]
    private static partial void LogErro(ILogger logger, string message, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogIniciada(logger);
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                PtaxIngestor ingestor = scope.ServiceProvider.GetRequiredService<PtaxIngestor>();
                await ingestor.IngestirAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErro(logger, ex.Message, ex);
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}

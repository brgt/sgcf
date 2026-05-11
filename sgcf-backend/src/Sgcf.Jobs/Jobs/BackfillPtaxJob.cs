using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Sgcf.Infrastructure.Bcb;

namespace Sgcf.Jobs.Jobs;

internal sealed partial class BackfillPtaxJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<BackfillPtaxJob> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Backfill PTAX iniciado: {Inicio} a {Fim}.")]
    private static partial void LogIniciado(ILogger logger, LocalDate inicio, LocalDate fim);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backfill PTAX concluído.")]
    private static partial void LogConcluido(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro no backfill PTAX: {Mensagem}")]
    private static partial void LogErro(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;
        LocalDate inicio = hoje.PlusDays(-30);
        LocalDate fim = hoje.PlusDays(-1);

        LogIniciado(logger, inicio, fim);

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            PtaxIngestor ingestor = scope.ServiceProvider.GetRequiredService<PtaxIngestor>();
            await ingestor.IngestirPeriodoAsync(inicio, fim, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogErro(logger, ex.Message, ex);
        }

        LogConcluido(logger);
        // Job exits naturally after one run — no loop
    }
}

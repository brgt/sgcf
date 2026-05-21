using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Sgcf.Application.Exportacao;
using Sgcf.Domain.Exportacao;

namespace Sgcf.Infrastructure.Jobs;

/// <summary>
/// Serviço de background que varre os <see cref="ExportacaoJob"/> com status
/// <see cref="StatusExportacao.Pendente"/> a cada 10 segundos e os processa.
///
/// Cada tenant tem o filtro global de tenant aplicado pelo <c>SgcfDbContext</c>,
/// por isso <see cref="IExportacaoJobRepository.ListPendentesAsync"/> retorna apenas
/// os jobs do tenant cujo contexto está ativo no scope corrente.
///
/// MVP: o resultado é um JSON simples com metadados do job.
/// Em versões futuras, cada <see cref="TipoExportacao"/> deve rotear para um exporter dedicado.
/// </summary>
internal sealed partial class ExportacaoProcessorService(
    IServiceProvider serviceProvider,
    IClock clock,
    ILogger<ExportacaoProcessorService> logger) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar ExportacaoJob {JobId}.")]
    private partial void LogJobFalhou(Exception ex, Guid jobId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(10));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessPendingJobsAsync(stoppingToken);
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        IExportacaoJobRepository repo = scope.ServiceProvider
            .GetRequiredService<IExportacaoJobRepository>();

        IReadOnlyList<ExportacaoJob> pendentes =
            await repo.ListPendentesAsync(cancellationToken);

        foreach (ExportacaoJob job in pendentes)
        {
            Instant agora = clock.GetCurrentInstant();

            try
            {
                job.IniciarProcessamento(agora);
                await repo.SaveChangesAsync(cancellationToken);

                // MVP: gera um JSON de resultado com metadados do job.
                // Versões futuras devem rotear por TipoExportacao para exporters especializados.
                string resultado = System.Text.Json.JsonSerializer.Serialize(new
                {
                    jobId = job.Id,
                    tipo = job.Tipo.ToString(),
                    parametros = job.ParametrosJson,
                    geradoEm = agora.ToString()
                });

                job.Concluir(resultado, clock.GetCurrentInstant());
                await repo.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                LogJobFalhou(ex, job.Id);
                job.Falhar(ex.Message, clock.GetCurrentInstant());
                await repo.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

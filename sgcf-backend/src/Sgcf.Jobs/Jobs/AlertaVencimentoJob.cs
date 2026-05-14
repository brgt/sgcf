using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Alertas;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Jobs.Jobs;

/// <summary>
/// Job diário que cria alertas de vencimento para eventos de cronograma nos horizontes D-7, D-3 e D-0.
/// Roda imediatamente ao iniciar e a cada 24 horas.
/// Idempotente: não recria alertas já existentes para a mesma (contrato, horizonte, data).
/// </summary>
internal sealed partial class AlertaVencimentoJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<AlertaVencimentoJob> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    // Brazil timezone: UTC-3 (America/Sao_Paulo — aware of DST)
    private static readonly DateTimeZone FusoHorarioBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    private static readonly string[] Horizontes = ["D_MENOS_7", "D_MENOS_3", "D_MENOS_0"];
    private static readonly int[] OffsetsDias = [7, 3, 0];

    [LoggerMessage(Level = LogLevel.Information,
        Message = "AlertaVencimentoJob: iniciando ciclo para {Hoje}.")]
    private static partial void LogIniciandoCiclo(ILogger logger, LocalDate hoje);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "AlertaVencimentoJob: alerta criado — contrato {ContratoId} tipo {TipoAlerta} vencimento {DataVencimento}.")]
    private static partial void LogAlertaCriado(ILogger logger, Guid contratoId, string tipoAlerta, LocalDate dataVencimento);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "AlertaVencimentoJob: erro inesperado: {Mensagem}")]
    private static partial void LogErroGeral(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarAlertasAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroGeral(logger, ex.Message, ex);
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task ProcessarAlertasAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        IEventoCronogramaRepository cronogramaRepo = sp.GetRequiredService<IEventoCronogramaRepository>();
        IAlertaVencimentoRepository alertaRepo = sp.GetRequiredService<IAlertaVencimentoRepository>();

        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoHorarioBrasilia).Date;
        LogIniciandoCiclo(logger, hoje);

        for (int i = 0; i < Horizontes.Length; i++)
        {
            string tipoAlerta = Horizontes[i];
            LocalDate dataAlvo = hoje.PlusDays(OffsetsDias[i]);

            IReadOnlyList<EventoCronograma> eventos =
                await cronogramaRepo.ListPendentesVencendoEmAsync(dataAlvo, cancellationToken);

            foreach (EventoCronograma evento in eventos)
            {
                bool jaExiste = await alertaRepo.ExisteAsync(
                    evento.ContratoId, tipoAlerta, evento.DataPrevista, cancellationToken);

                if (jaExiste)
                {
                    continue;
                }

                AlertaVencimento alerta = AlertaVencimento.Criar(
                    evento.ContratoId,
                    tipoAlerta,
                    evento.DataPrevista,
                    hoje,
                    evento.ValorMoedaOriginal,
                    clock);

                alertaRepo.Add(alerta);
                await alertaRepo.SaveChangesAsync(cancellationToken);

                LogAlertaCriado(logger, evento.ContratoId, tipoAlerta, evento.DataPrevista);
                SgcfJobsMetrics.AlertaVencimentoCriado.Add(1);
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Alertas;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;

namespace Sgcf.Jobs.Jobs;

/// <summary>
/// Job diário que verifica a exposição de crédito de cada banco com limite configurado.
/// Cria um <see cref="AlertaExposicaoBanco"/> quando a exposição atingir >= 80% do limite.
/// Roda imediatamente ao iniciar e a cada 24 horas.
/// Idempotente: não recria alertas para o mesmo (banco, data).
/// </summary>
internal sealed partial class AlertaExposicaoBancoJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<AlertaExposicaoBancoJob> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private static readonly DateTimeZone FusoHorarioBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // Alert threshold: 80% of limit
    private const decimal LimiarOcupacao = 0.80m;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "AlertaExposicaoBancoJob: verificando {Count} bancos com limite configurado para {Hoje}.")]
    private static partial void LogVerificandoBancos(ILogger logger, int count, LocalDate hoje);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AlertaExposicaoBancoJob: alerta criado — banco {BancoId} exposição {PercentualOcupacao:P2} do limite em {DataAlerta}.")]
    private static partial void LogAlertaCriado(ILogger logger, Guid bancoId, decimal percentualOcupacao, LocalDate dataAlerta);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "AlertaExposicaoBancoJob: erro inesperado: {Mensagem}")]
    private static partial void LogErroGeral(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarExposicaoAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroGeral(logger, ex.Message, ex);
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task ProcessarExposicaoAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        IBancoRepository bancoRepo = sp.GetRequiredService<IBancoRepository>();
        IContratoRepository contratoRepo = sp.GetRequiredService<IContratoRepository>();
        IAlertaExposicaoBancoRepository alertaRepo = sp.GetRequiredService<IAlertaExposicaoBancoRepository>();

        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoHorarioBrasilia).Date;

        IReadOnlyList<Banco> bancos = await bancoRepo.ListComLimiteCreditoSetadoAsync(cancellationToken);
        LogVerificandoBancos(logger, bancos.Count, hoje);

        foreach (Banco banco in bancos)
        {
            // LimiteCreditoBrl is guaranteed non-null because ListComLimiteCreditoSetadoAsync filters for it
            Money limiteBrl = banco.LimiteCreditoBrl!.Value;

            decimal exposicaoDecimal =
                await contratoRepo.GetSaldoPrincipalTotalPorBancoAsync(banco.Id, cancellationToken);

            decimal percentual = Math.Round(
                limiteBrl.Valor > 0 ? exposicaoDecimal / limiteBrl.Valor : 0m,
                6, MidpointRounding.AwayFromZero);

            if (percentual < LimiarOcupacao)
            {
                continue;
            }

            bool jaExiste = await alertaRepo.ExisteAsync(banco.Id, hoje, cancellationToken);
            if (jaExiste)
            {
                continue;
            }

            Money exposicaoBrl = new(exposicaoDecimal, Moeda.Brl);

            AlertaExposicaoBanco alerta = AlertaExposicaoBanco.Criar(
                banco.Id, hoje, exposicaoBrl, limiteBrl, clock);

            alertaRepo.Add(alerta);
            await alertaRepo.SaveChangesAsync(cancellationToken);

            LogAlertaCriado(logger, banco.Id, percentual, hoje);
            SgcfJobsMetrics.AlertaExposicaoCriado.Add(1);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Cotacoes;
using Sgcf.Domain.Hedge;

namespace Sgcf.Jobs.Jobs;

/// <summary>
/// Executes daily after PTAX D0 is published (~14h BRT = 17h UTC).
/// For each active hedge expiring today, calculates the final MTM using PTAX D0,
/// persists a liquidation snapshot, and marks the hedge as LIQUIDADO.
/// </summary>
internal sealed partial class LiquidarNdfJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<LiquidarNdfJob> logger) : BackgroundService
{
    // Target time: 14h00 BRT = 17h00 UTC
    private static readonly TimeOnly HoraExecucaoUtc = new(17, 0);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "LiquidarNdfJob: processando {Count} hedges vencendo hoje ({Data}).")]
    private static partial void LogProcessando(ILogger logger, int count, LocalDate data);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "LiquidarNdfJob: PTAX D0 indisponível para {Moeda} — hedge {HedgeId} não liquidado.")]
    private static partial void LogPtaxIndisponivel(ILogger logger, string moeda, Guid hedgeId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "LiquidarNdfJob: hedge {HedgeId} liquidado — MTM = {Mtm:F2} BRL.")]
    private static partial void LogLiquidado(ILogger logger, Guid hedgeId, decimal mtm);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "LiquidarNdfJob: erro ao liquidar hedge {HedgeId}: {Mensagem}")]
    private static partial void LogErro(ILogger logger, Guid hedgeId, string mensagem, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "LiquidarNdfJob: erro inesperado: {Mensagem}")]
    private static partial void LogErroGeral(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime agoraUtc = clock.GetCurrentInstant().ToDateTimeUtc();

            // Calculate delay until next 17h00 UTC (14h00 BRT)
            DateTime proximaExecucao = agoraUtc.TimeOfDay < HoraExecucaoUtc.ToTimeSpan()
                ? agoraUtc.Date.Add(HoraExecucaoUtc.ToTimeSpan())
                : agoraUtc.Date.AddDays(1).Add(HoraExecucaoUtc.ToTimeSpan());

            TimeSpan delay = proximaExecucao - agoraUtc;
            await Task.Delay(delay, stoppingToken);

            try
            {
                await LiquidarAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroGeral(logger, ex.Message, ex);
            }
        }
    }

    private async Task LiquidarAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;
        IHedgeRepository hedgeRepo = sp.GetRequiredService<IHedgeRepository>();
        ICotacaoFxRepository cotacaoRepo = sp.GetRequiredService<ICotacaoFxRepository>();

        Instant agora = clock.GetCurrentInstant();
        LocalDate hoje = agora.InUtc().Date;

        IReadOnlyList<InstrumentoHedge> hedges = await hedgeRepo.ListAtivosVencendoEmAsync(hoje, cancellationToken);
        LogProcessando(logger, hedges.Count, hoje);

        foreach (InstrumentoHedge hedge in hedges)
        {
            try
            {
                await LiquidarHedgeAsync(hedge, hoje, agora, hedgeRepo, cotacaoRepo, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErro(logger, hedge.Id, ex.Message, ex);
            }
        }
    }

    private async Task LiquidarHedgeAsync(
        InstrumentoHedge hedge,
        LocalDate hoje,
        Instant agora,
        IHedgeRepository hedgeRepo,
        ICotacaoFxRepository cotacaoRepo,
        CancellationToken cancellationToken)
    {
        Domain.Common.Moeda moedaBase = hedge.MoedaBase;

        Domain.Cotacoes.CotacaoFx? ptax = await cotacaoRepo.GetMaisRecenteAsync(
            moedaBase, TipoCotacao.PtaxD0, hoje, cancellationToken);

        if (ptax is null)
        {
            LogPtaxIndisponivel(logger, moedaBase.ToString(), hedge.Id);
            return;
        }

        // Mid-point of buy/sell for NDF liquidation fixing
        decimal spotFixing = Math.Round(
            (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
            6, MidpointRounding.AwayFromZero);

        decimal notional = hedge.Notional.Valor;

        decimal mtm = hedge.Tipo == TipoHedge.NdfForward
            ? NdfMtmCalculador.CalcularMtmForward(notional, hedge.StrikeForward!.Value, spotFixing)
            : NdfMtmCalculador.CalcularMtmCollar(notional, hedge.StrikePut!.Value, hedge.StrikeCall!.Value, spotFixing);

        PosicaoSnapshot snapshot = PosicaoSnapshot.CriarComInstant(
            hedge.Id, hedge.ContratoId, mtm, spotFixing, "PTAX_D0", agora);

        hedgeRepo.AddSnapshot(snapshot);
        hedge.Liquidar();
        await hedgeRepo.SaveChangesAsync(cancellationToken);

        LogLiquidado(logger, hedge.Id, mtm);
    }
}

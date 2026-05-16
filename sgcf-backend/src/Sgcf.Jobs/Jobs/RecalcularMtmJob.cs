using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Application.Hedge;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Hedge;

namespace Sgcf.Jobs.Jobs;

internal sealed partial class RecalcularMtmJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<RecalcularMtmJob> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    // Market hours in BRT (UTC-3): 9h–18h = 12h–21h UTC
    private static readonly TimeOnly InicioMercadoUtc = new(12, 0);
    private static readonly TimeOnly FimMercadoUtc = new(21, 0);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RecalcularMtmJob: processando {Count} hedges ativos.")]
    private static partial void LogProcessando(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RecalcularMtmJob: spot cache vazio para {Moeda} — usando PTAX D-1 como fallback.")]
    private static partial void LogUsandoFallbackPtax(ILogger logger, string moeda);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RecalcularMtmJob: cotação indisponível para {Moeda} — hedge {HedgeId} ignorado.")]
    private static partial void LogCotacaoIndisponivel(ILogger logger, string moeda, Guid hedgeId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RecalcularMtmJob: erro ao processar hedge {HedgeId}: {Mensagem}")]
    private static partial void LogErroHedge(ILogger logger, Guid hedgeId, string mensagem, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RecalcularMtmJob: erro inesperado: {Mensagem}")]
    private static partial void LogErroGeral(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Intervalo, stoppingToken);

            Instant agora = clock.GetCurrentInstant();
            TimeOnly horaUtc = TimeOnly.FromTimeSpan(agora.ToDateTimeUtc().TimeOfDay);

            // Only run during BRT market hours (12h–21h UTC = 9h–18h BRT)
            if (horaUtc < InicioMercadoUtc || horaUtc > FimMercadoUtc)
            {
                continue;
            }

            try
            {
                await RecalcularAsync(agora, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroGeral(logger, ex.Message, ex);
            }
        }
    }

    private async Task RecalcularAsync(Instant agora, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;
        IHedgeRepository hedgeRepo = sp.GetRequiredService<IHedgeRepository>();
        ICotacaoSpotCache spotCache = sp.GetRequiredService<ICotacaoSpotCache>();
        ICotacaoFxRepository cotacaoRepo = sp.GetRequiredService<ICotacaoFxRepository>();
        LocalDate hoje = agora.InUtc().Date;

        IReadOnlyList<InstrumentoHedge> hedges = await hedgeRepo.ListAtivosAsync(cancellationToken);
        LogProcessando(logger, hedges.Count);

        foreach (InstrumentoHedge hedge in hedges)
        {
            // Idempotency guard: skip if a snapshot was already written within the last 4 minutes
            PosicaoSnapshot? ultimo = await hedgeRepo.GetSnapshotMaisRecenteAsync(hedge.Id, cancellationToken);
            if (ultimo is not null && (agora - ultimo.CalculadoEm) < Duration.FromMinutes(4))
            {
                continue;
            }

            try
            {
                await ProcessarHedgeAsync(hedge, agora, hoje, hedgeRepo, spotCache, cotacaoRepo, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroHedge(logger, hedge.Id, ex.Message, ex);
            }
        }
    }

    private async Task ProcessarHedgeAsync(
        InstrumentoHedge hedge,
        Instant agora,
        LocalDate hoje,
        IHedgeRepository hedgeRepo,
        ICotacaoSpotCache spotCache,
        ICotacaoFxRepository cotacaoRepo,
        CancellationToken cancellationToken)
    {
        (decimal spotValor, string tipoCotacao)? cotacao =
            await ResolverCotacaoAsync(hedge, hoje, spotCache, cotacaoRepo, cancellationToken);

        if (cotacao is null)
        {
            LogCotacaoIndisponivel(logger, hedge.MoedaBase.ToString(), hedge.Id);
            return;
        }

        decimal notional = hedge.Notional.Valor;

        decimal mtm = hedge.Tipo == TipoHedge.NdfForward
            ? NdfMtmCalculador.CalcularMtmForward(notional, hedge.StrikeForward!.Value, cotacao.Value.spotValor)
            : NdfMtmCalculador.CalcularMtmCollar(notional, hedge.StrikePut!.Value, hedge.StrikeCall!.Value, cotacao.Value.spotValor);

        PosicaoSnapshot snapshot = PosicaoSnapshot.CriarComInstant(
            hedge.Id, hedge.ContratoId, mtm, cotacao.Value.spotValor, cotacao.Value.tipoCotacao, agora);

        hedgeRepo.AddSnapshot(snapshot);
        await hedgeRepo.SaveChangesAsync(cancellationToken);

        SgcfJobsMetrics.MtmRecalculadoTotal.Add(1);
    }

    private async Task<(decimal spotValor, string tipoCotacao)?> ResolverCotacaoAsync(
        InstrumentoHedge hedge,
        LocalDate hoje,
        ICotacaoSpotCache spotCache,
        ICotacaoFxRepository cotacaoRepo,
        CancellationToken cancellationToken)
    {
        // Primary: Redis intraday spot cache
        Money? spot = await spotCache.GetSpotAsync(hedge.MoedaBase, cancellationToken);
        if (spot is not null)
        {
            return (spot.Value.Valor, "SPOT_INTRADAY");
        }

        // Fallback: last known PTAX D-1 from the database (BCB API may be unavailable)
        LogUsandoFallbackPtax(logger, hedge.MoedaBase.ToString());

        CotacaoFx? ptax = await cotacaoRepo.GetMaisRecenteAsync(
            hedge.MoedaBase, TipoCotacao.PtaxD1, hoje, cancellationToken);

        if (ptax is not null)
        {
            decimal mid = Math.Round(
                (ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m,
                6, MidpointRounding.AwayFromZero);
            return (mid, "PTAX_D1_FALLBACK");
        }

        return null;
    }
}

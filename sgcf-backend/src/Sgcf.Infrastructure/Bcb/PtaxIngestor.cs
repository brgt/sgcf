using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;

namespace Sgcf.Infrastructure.Bcb;

public sealed partial class PtaxIngestor(
    BcbPtaxClient bcbClient,
    ICotacaoFxRepository cotacaoRepo,
    IClock clock,
    ILogger<PtaxIngestor> logger)
{
    private static readonly IReadOnlyList<Moeda> MoedasSuportadas = new[] { Moeda.Usd, Moeda.Eur, Moeda.Jpy, Moeda.Cny };

    private static readonly DateTimeZone ZonaBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    private static readonly LocalDateTimePattern PatternDataHora =
        LocalDateTimePattern.CreateWithInvariantCulture("yyyy-MM-dd HH:mm:ss.fff");

    [LoggerMessage(Level = LogLevel.Information, Message = "Iniciando ingestão PTAX para {Moeda} em {Data}.")]
    private static partial void LogIniciandoMoeda(ILogger logger, Moeda moeda, LocalDate data);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nenhum boletim retornado pela BCB para {Moeda} em {Data}.")]
    private static partial void LogSemBoletins(ILogger logger, Moeda moeda, LocalDate data);

    [LoggerMessage(Level = LogLevel.Information, Message = "PTAX upsert concluído: {Moeda} tipo={Tipo} momento={Momento}.")]
    private static partial void LogUpsertConcluido(ILogger logger, Moeda moeda, TipoCotacao tipo, Instant momento);

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro ao ingerir PTAX para {Moeda}: {Erro}")]
    private static partial void LogErroPorMoeda(ILogger logger, Moeda moeda, string erro, Exception ex);

    public async Task IngestirAsync(CancellationToken cancellationToken)
    {
        LocalDate dataAtual = clock.GetCurrentInstant().InUtc().Date;

        foreach (Moeda moeda in MoedasSuportadas)
        {
            try
            {
                await IngestirMoedaAsync(moeda, dataAtual, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroPorMoeda(logger, moeda, ex.Message, ex);
            }
        }
    }

    public async Task IngestirPeriodoAsync(LocalDate inicio, LocalDate fim, CancellationToken cancellationToken)
    {
        LocalDate data = inicio;
        while (data <= fim)
        {
            foreach (Moeda moeda in MoedasSuportadas)
            {
                try
                {
                    await IngestirMoedaAsync(moeda, data, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogErroPorMoeda(logger, moeda, ex.Message, ex);
                }
            }
            data = data.PlusDays(1);
        }
    }

    private async Task IngestirMoedaAsync(Moeda moeda, LocalDate dataAtual, CancellationToken cancellationToken)
    {
        LogIniciandoMoeda(logger, moeda, dataAtual);

        IReadOnlyList<BcbBoletim> boletins = await bcbClient.GetBoletinsAsync(moeda, dataAtual, cancellationToken);

        if (boletins.Count == 0)
        {
            LogSemBoletins(logger, moeda, dataAtual);
            return;
        }

        // Fechamento boletins → PtaxD0
        foreach (BcbBoletim boletim in boletins)
        {
            if (boletim.TipoBoletim == "Fechamento")
            {
                Instant momento = ParseDataHora(boletim.DataHoraCotacao);
                CotacaoFx cotacao = CotacaoFx.Criar(
                    moeda,
                    TipoCotacao.PtaxD0,
                    new Money(boletim.CotacaoCompra, Moeda.Brl),
                    new Money(boletim.CotacaoVenda, Moeda.Brl),
                    "BCB_OLINDA",
                    momento);

                await cotacaoRepo.UpsertAsync(cotacao, cancellationToken);
                LogUpsertConcluido(logger, moeda, TipoCotacao.PtaxD0, momento);
            }
        }

        // Boletim mais recente de qualquer tipo → SpotIntraday
        BcbBoletim? maisRecente = null;
        Instant momentoMaisRecente = Instant.MinValue;

        foreach (BcbBoletim boletim in boletins)
        {
            Instant momentoBoletim = ParseDataHora(boletim.DataHoraCotacao);
            if (momentoBoletim > momentoMaisRecente)
            {
                momentoMaisRecente = momentoBoletim;
                maisRecente = boletim;
            }
        }

        if (maisRecente is not null)
        {
            CotacaoFx spotIntraday = CotacaoFx.Criar(
                moeda,
                TipoCotacao.SpotIntraday,
                new Money(maisRecente.CotacaoCompra, Moeda.Brl),
                new Money(maisRecente.CotacaoVenda, Moeda.Brl),
                "BCB_OLINDA",
                momentoMaisRecente);

            await cotacaoRepo.UpsertAsync(spotIntraday, cancellationToken);
            LogUpsertConcluido(logger, moeda, TipoCotacao.SpotIntraday, momentoMaisRecente);
        }
    }

    private static Instant ParseDataHora(string dataHora)
    {
        LocalDateTime ldt = PatternDataHora.Parse(dataHora).Value;
        return ldt.InZoneLeniently(ZonaBrasilia).ToInstant();
    }
}

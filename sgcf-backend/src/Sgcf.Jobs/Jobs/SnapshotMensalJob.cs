using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Painel;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Painel;

namespace Sgcf.Jobs.Jobs;

/// <summary>
/// Job diário que cria um <see cref="SnapshotMensalPosicao"/> no último dia de cada mês.
/// Captura: contratos ativos, saldo principal em BRL (PTAX D-1 para conversão FX) e total de parcelas abertas em BRL.
/// Roda imediatamente ao iniciar e a cada 24 horas.
/// Idempotente: não cria snapshot duplicado para o mesmo (ano, mes).
/// </summary>
internal sealed partial class SnapshotMensalJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<SnapshotMensalJob> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private static readonly DateTimeZone FusoHorarioBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SnapshotMensalJob: {Hoje} não é o último dia do mês — nenhuma ação.")]
    private static partial void LogNaoEUltimoDia(ILogger logger, LocalDate hoje);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SnapshotMensalJob: snapshot {Ano}/{Mes} já existe — nenhuma ação.")]
    private static partial void LogJaExiste(ILogger logger, int ano, int mes);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SnapshotMensalJob: snapshot {Ano}/{Mes} criado — {TotalContratos} contratos, saldo BRL {SaldoBrl:F2}, parcelas abertas BRL {ParcelasAbertasBrl:F2}.")]
    private static partial void LogSnapshotCriado(ILogger logger, int ano, int mes, int totalContratos, decimal saldoBrl, decimal parcelasAbertasBrl);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SnapshotMensalJob: PTAX D-1 indisponível para {Moeda} — usando zero na conversão.")]
    private static partial void LogPtaxIndisponivel(ILogger logger, string moeda);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "SnapshotMensalJob: erro inesperado: {Mensagem}")]
    private static partial void LogErroGeral(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarSnapshotAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroGeral(logger, ex.Message, ex);
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task ProcessarSnapshotAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        IContratoRepository contratoRepo = sp.GetRequiredService<IContratoRepository>();
        IEventoCronogramaRepository cronogramaRepo = sp.GetRequiredService<IEventoCronogramaRepository>();
        ICotacaoFxRepository cotacaoRepo = sp.GetRequiredService<ICotacaoFxRepository>();
        ISnapshotMensalPosicaoRepository snapshotRepo = sp.GetRequiredService<ISnapshotMensalPosicaoRepository>();

        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoHorarioBrasilia).Date;

        // Check if today is the last day of the month: if tomorrow is a different month, today is the last day
        bool ehUltimoDia = hoje.PlusDays(1).Month != hoje.Month;
        if (!ehUltimoDia)
        {
            LogNaoEUltimoDia(logger, hoje);
            return;
        }

        bool jaExiste = await snapshotRepo.ExisteParaMesAsync(hoje.Year, hoje.Month, cancellationToken);
        if (jaExiste)
        {
            LogJaExiste(logger, hoje.Year, hoje.Month);
            return;
        }

        int totalAtivos = await contratoRepo.CountAtivosAsync(cancellationToken);

        IReadOnlyList<(Guid Id, decimal ValorPrincipal, Moeda Moeda)> valoresPrincipais =
            await contratoRepo.ListAtivosValoresPrincipaisAsync(cancellationToken);

        decimal saldoPrincipalBrl = await ConverterParaBrlAsync(
            valoresPrincipais.Select(v => (v.ValorPrincipal, v.Moeda)),
            hoje, cotacaoRepo, cancellationToken);

        IReadOnlyList<(decimal Valor, Moeda Moeda)> valoresPendentes =
            await cronogramaRepo.ListValoresPendentesAsync(cancellationToken);

        decimal totalParcelasAbertasBrl = await ConverterParaBrlAsync(
            valoresPendentes.Select(v => (v.Valor, v.Moeda)),
            hoje, cotacaoRepo, cancellationToken);

        SnapshotMensalPosicao snap = SnapshotMensalPosicao.Criar(
            hoje.Year,
            hoje.Month,
            totalAtivos,
            saldoPrincipalBrl,
            totalParcelasAbertasBrl,
            clock);

        snapshotRepo.Add(snap);
        await snapshotRepo.SaveChangesAsync(cancellationToken);

        LogSnapshotCriado(logger, hoje.Year, hoje.Month, totalAtivos, saldoPrincipalBrl, totalParcelasAbertasBrl);
        SgcfJobsMetrics.SnapshotMensalCriado.Add(1);
    }

    /// <summary>
    /// Soma todos os valores convertidos para BRL usando PTAX D-1.
    /// Valores já em BRL são adicionados diretamente. Valores em outras moedas usam
    /// a cotação PTAX D-1 mais recente disponível; se indisponível, contribuem com zero
    /// e um warning é emitido.
    /// </summary>
    private async Task<decimal> ConverterParaBrlAsync(
        IEnumerable<(decimal Valor, Moeda Moeda)> itens,
        LocalDate hoje,
        ICotacaoFxRepository cotacaoRepo,
        CancellationToken cancellationToken)
    {
        decimal totalBrl = 0m;

        // Cache PTAX rates by currency to avoid redundant DB calls within the same cycle
        Dictionary<Moeda, decimal?> taxaCache = [];

        foreach ((decimal valor, Moeda moeda) in itens)
        {
            if (moeda == Moeda.Brl)
            {
                totalBrl += valor;
                continue;
            }

            if (!taxaCache.TryGetValue(moeda, out decimal? taxa))
            {
                CotacaoFx? ptax = await cotacaoRepo.GetMaisRecenteAsync(moeda, TipoCotacao.PtaxD1, hoje, cancellationToken);
                taxa = ptax is not null
                    ? Math.Round((ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m, 6, MidpointRounding.AwayFromZero)
                    : (decimal?)null;

                taxaCache[moeda] = taxa;

                if (taxa is null)
                {
                    LogPtaxIndisponivel(logger, moeda.ToString());
                }
            }

            if (taxa is null)
            {
                continue;
            }

            totalBrl += Math.Round(valor * taxa.Value, 6, MidpointRounding.AwayFromZero);
        }

        return Math.Round(totalBrl, 6, MidpointRounding.AwayFromZero);
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contabilidade;
using Sgcf.Domain.Contratos;

namespace Sgcf.Jobs.Jobs;

/// <summary>
/// Job diário que calcula a provisão de juros pro rata de cada contrato ativo
/// e cria um <see cref="LancamentoContabil"/> por contrato por dia.
/// Roda imediatamente ao iniciar e a cada 24 horas.
/// Idempotente: não recria lançamentos existentes para a mesma (contrato, data, origem).
/// </summary>
internal sealed partial class ProvisaoJurosDiariaJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<ProvisaoJurosDiariaJob> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private static readonly DateTimeZone FusoHorarioBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    private const string OrigemProvisao = "PROVISAO_JUROS";

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ProvisaoJurosDiariaJob: processando {Count} contratos ativos para {Hoje}.")]
    private static partial void LogProcessando(ILogger logger, int count, LocalDate hoje);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ProvisaoJurosDiariaJob: lançamento criado — contrato {ContratoId} valor {Valor} {MoedaNome} em {Data}.")]
    private static partial void LogLancamentoCriado(ILogger logger, Guid contratoId, decimal valor, string moedaNome, LocalDate data);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "ProvisaoJurosDiariaJob: erro ao processar contrato {ContratoId}: {Mensagem}")]
    private static partial void LogErroContrato(ILogger logger, Guid contratoId, string mensagem, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "ProvisaoJurosDiariaJob: erro inesperado: {Mensagem}")]
    private static partial void LogErroGeral(ILogger logger, string mensagem, Exception ex);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarProvisaoAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroGeral(logger, ex.Message, ex);
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task ProcessarProvisaoAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        IContratoRepository contratoRepo = sp.GetRequiredService<IContratoRepository>();
        ILancamentoContabilRepository lancamentoRepo = sp.GetRequiredService<ILancamentoContabilRepository>();

        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoHorarioBrasilia).Date;

        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAtivosComTaxaAsync(cancellationToken);
        LogProcessando(logger, contratos.Count, hoje);

        foreach (Contrato contrato in contratos)
        {
            try
            {
                await ProcessarContratoAsync(contrato, hoje, lancamentoRepo, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogErroContrato(logger, contrato.Id, ex.Message, ex);
            }
        }
    }

    private async Task ProcessarContratoAsync(
        Contrato contrato,
        LocalDate hoje,
        ILancamentoContabilRepository lancamentoRepo,
        CancellationToken cancellationToken)
    {
        bool jaExiste = await lancamentoRepo.ExisteAsync(contrato.Id, hoje, OrigemProvisao, cancellationToken);
        if (jaExiste)
        {
            return;
        }

        // ValorPrincipalDecimal and TaxaAaDecimal are internal — access via public Money/Percentual properties
        decimal principalDecimal = contrato.ValorPrincipal.Valor;
        decimal taxaAaDecimal = contrato.TaxaAa.AsDecimal;

        decimal jurosDiario = CalcularJurosDiario(principalDecimal, taxaAaDecimal, contrato.BaseCalculo);

        Money valorJuros = new(jurosDiario, contrato.Moeda);

        // Build description without string interpolation in log call — CA1873 prevention
        string descricao = string.Concat(
            "Provisão de juros diária — contrato ", contrato.NumeroExterno, " — ", hoje.ToString());

        LancamentoContabil lancamento = LancamentoContabil.Criar(
            contrato.Id, hoje, OrigemProvisao, valorJuros, descricao, clock);

        lancamentoRepo.Add(lancamento);
        await lancamentoRepo.SaveChangesAsync(cancellationToken);

        string moedaNome = contrato.Moeda.ToString();
        LogLancamentoCriado(logger, contrato.Id, jurosDiario, moedaNome, hoje);
        SgcfJobsMetrics.ProvisaoJurosCriada.Add(1);
    }

    /// <summary>
    /// Calcula o juros diário pro rata por capitalização composta.
    /// Fórmula: principal * ((1 + taxa_aa)^(1/base) - 1)
    /// onde base = 252, 360 ou 365 conforme BaseCalculo.
    /// </summary>
    private static decimal CalcularJurosDiario(decimal principal, decimal taxaAa, BaseCalculo baseCalculo)
    {
        double base_ = (double)(int)baseCalculo;

        // Compound daily factor: (1 + taxa_aa)^(1/base) - 1
        double fatorDiario = Math.Pow(1.0 + (double)taxaAa, 1.0 / base_) - 1.0;

        decimal jurosDiario = principal * (decimal)fatorDiario;

        return Math.Round(jurosDiario, 6, MidpointRounding.AwayFromZero);
    }
}

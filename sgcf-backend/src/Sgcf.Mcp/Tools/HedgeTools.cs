using System.ComponentModel;
using System.Text.Json;
using MediatR;
using ModelContextProtocol.Server;
using Sgcf.Application.Hedge;
using Sgcf.Application.Hedge.Queries;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;

namespace Sgcf.Mcp.Tools;

[McpServerToolType]
public sealed class HedgeTools(IMediator mediator)
{
    [McpServerTool(Name = "get_mtm_hedge")]
    [Description("Retorna o Mark-to-Market (MTM) de um instrumento de hedge (NDF Forward ou Collar) usando a cotação spot atual.")]
    public async Task<string> GetMtmHedgeAsync(
        [Description("UUID do instrumento de hedge.")] string hedgeId,
        [Description("Saldo notional do contrato vinculado em BRL (para cálculo de cobertura). Padrão: 0.")] decimal notionalContratoSaldo,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(hedgeId, out Guid id))
        {
            return JsonSerializer.Serialize(new { error = "hedgeId inválido — esperado UUID." }, McpJsonOptions.Default);
        }

        try
        {
            MtmResultadoDto result = await mediator.Send(new GetMtmQuery(id, notionalContratoSaldo), cancellationToken);
            return JsonSerializer.Serialize(result, McpJsonOptions.Default);
        }
        catch (KeyNotFoundException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJsonOptions.Default);
        }
        catch (InvalidOperationException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJsonOptions.Default);
        }
    }

    [McpServerTool(Name = "simular_cenario_cambial")]
    [Description("Simula o impacto de variações cambiais sobre a dívida total em BRL. Retorna 4 cenários: customizado, pessimista (-10%), realista (0%) e otimista (+10%). Os deltas são percentuais (ex: -10 para -10%).")]
    public async Task<string> SimularCenarioCambialAsync(
        [Description("Variação percentual do USD (ex: -5 para -5%). Null = sem variação.")] decimal? deltaUsdPct,
        [Description("Variação percentual do EUR. Null = sem variação.")] decimal? deltaEurPct,
        [Description("Variação percentual do JPY. Null = sem variação.")] decimal? deltaJpyPct,
        [Description("Variação percentual do CNY. Null = sem variação.")] decimal? deltaCnyPct,
        CancellationToken cancellationToken)
    {
        ResultadoCenarioCambialDto result = await mediator.Send(
            new SimularCenarioCambialQuery(deltaUsdPct, deltaEurPct, deltaJpyPct, deltaCnyPct),
            cancellationToken);
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool(Name = "simular_antecipacao")]
    [Description("Simula a antecipação do portfólio de contratos dado um caixa disponível e uma taxa CDI de referência. Retorna os top-5 contratos com maior economia líquida (após custo de oportunidade). Contratos Sicredi são excluídos pois não geram economia.")]
    public async Task<string> SimularAntecipacaoAsync(
        [Description("Caixa disponível para antecipação em BRL (ex: 5000000).")] decimal caixaDisponivelBrl,
        [Description("Taxa CDI anual de referência como percentual (ex: 10.75 para 10,75% a.a.). Null = usa taxa do sistema.")] decimal? taxaCdiAa,
        CancellationToken cancellationToken)
    {
        ResultadoAntecipacaoPortfolioDto result = await mediator.Send(
            new SimularAntecipacaoPortfolioQuery(caixaDisponivelBrl, taxaCdiAa),
            cancellationToken);
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }
}

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using MediatR;
using ModelContextProtocol.Server;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Painel.Queries;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Mcp.Tools;

[McpServerToolType]
public sealed class DividaTools(
    IMediator mediator,
    ICotacaoSpotCache spotCache,
    ICotacaoFxRepository cotacaoRepo,
    IClock clock)
{
    [McpServerTool(Name = "get_posicao_divida")]
    [Description("Retorna a posição consolidada de dívida em múltiplas moedas, com ajuste MTM de hedges e alertas de contratos sem cobertura cambial.")]
    public async Task<string> GetPosicaoDividaAsync(
        [Description("UUID do banco para filtrar (opcional).")] string? bancoId,
        [Description("Modalidade para filtrar: Finimp, Lei4131, Refinimp, Nce, BalcaoCaixa, Fgi (opcional).")] string? modalidade,
        CancellationToken cancellationToken)
    {
        Guid? bancoGuid = null;
        if (!string.IsNullOrEmpty(bancoId))
        {
            if (!Guid.TryParse(bancoId, out Guid parsed))
            {
                return JsonSerializer.Serialize(new { error = "bancoId inválido — esperado UUID." }, McpJsonOptions.Default);
            }

            bancoGuid = parsed;
        }

        Sgcf.Application.Painel.PainelDividaDto result = await mediator.Send(new GetPainelDividaQuery(bancoGuid, modalidade), cancellationToken);
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool(Name = "get_calendario_vencimentos")]
    [Description("Retorna o calendário de vencimentos de parcelas abertas para um ano civil, agrupadas por mês com totais em BRL.")]
    public async Task<string> GetCalendarioVencimentosAsync(
        [Description("Ano civil (ex: 2026).")] int ano,
        [Description("UUID do banco para filtrar (opcional).")] string? bancoId,
        [Description("Modalidade para filtrar (opcional).")] string? modalidade,
        [Description("Código da moeda para filtrar: USD, EUR, BRL etc. (opcional).")] string? moeda,
        CancellationToken cancellationToken)
    {
        Guid? bancoGuid = null;
        if (!string.IsNullOrEmpty(bancoId))
        {
            if (!Guid.TryParse(bancoId, out Guid parsed))
            {
                return JsonSerializer.Serialize(new { error = "bancoId inválido — esperado UUID." }, McpJsonOptions.Default);
            }

            bancoGuid = parsed;
        }

        Sgcf.Application.Painel.CalendarioVencimentosDto result = await mediator.Send(
            new GetCalendarioVencimentosQuery(ano, bancoGuid, modalidade, moeda),
            cancellationToken);
        return JsonSerializer.Serialize(result, McpJsonOptions.Default);
    }

    [McpServerTool(Name = "get_cotacao_fx")]
    [Description("Retorna a cotação atual de uma moeda estrangeira em BRL. Usa cache spot intraday (Redis); fallback para PTAX D-1 se o cache estiver vazio.")]
    public async Task<string> GetCotacaoFxAsync(
        [Description("Código da moeda: USD, EUR, JPY ou CNY.")] string moeda,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Moeda>(moeda, ignoreCase: true, out Moeda moedaEnum) || moedaEnum == Moeda.Brl)
        {
            return JsonSerializer.Serialize(
                new { error = "moeda inválida. Valores aceitos: USD, EUR, JPY, CNY." },
                McpJsonOptions.Default);
        }

        Sgcf.Domain.Common.Money? spot = await spotCache.GetSpotAsync(moedaEnum, cancellationToken);
        if (spot is not null)
        {
            return JsonSerializer.Serialize(
                new { moeda = moedaEnum.ToString(), valor_brl = spot.Value.Valor, tipo = "SPOT_INTRADAY" },
                McpJsonOptions.Default);
        }

        LocalDate hoje = clock.GetCurrentInstant().InUtc().Date;
        CotacaoFx? ptax = await cotacaoRepo.GetMaisRecenteAsync(moedaEnum, TipoCotacao.PtaxD1, hoje, cancellationToken);

        if (ptax is not null)
        {
            decimal mid = Math.Round((ptax.ValorCompra.Valor + ptax.ValorVenda.Valor) / 2m, 6, MidpointRounding.AwayFromZero);
            string momentoStr = ptax.Momento
                .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            return JsonSerializer.Serialize(
                new { moeda = moedaEnum.ToString(), valor_brl = mid, tipo = "PTAX_D1_FALLBACK", momento = momentoStr },
                McpJsonOptions.Default);
        }

        return JsonSerializer.Serialize(
            new { error = $"Cotação indisponível para {moeda}. Verifique o job de ingestão PTAX." },
            McpJsonOptions.Default);
    }
}

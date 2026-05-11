using MediatR;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Queries;

/// <summary>Retorna indicadores consolidados das garantias de um contrato.</summary>
public sealed record GetIndicadoresGarantiaQuery(Guid ContratoId) : IRequest<IndicadoresGarantiaDto>;

public sealed class GetIndicadoresGarantiaQueryHandler(
    IContratoRepository contratoRepo,
    IGarantiaRepository garantiaRepo)
    : IRequestHandler<GetIndicadoresGarantiaQuery, IndicadoresGarantiaDto>
{
    public async Task<IndicadoresGarantiaDto> Handle(
        GetIndicadoresGarantiaQuery query,
        CancellationToken cancellationToken)
    {
        Contrato contrato = await contratoRepo.GetByIdAsync(query.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{query.ContratoId}' não encontrado.");

        IReadOnlyList<Garantia> garantias = await garantiaRepo.ListByContratoAsync(
            query.ContratoId, cancellationToken);

        // Only active guarantees count toward coverage
        IReadOnlyList<Garantia> ativas = garantias
            .Where(g => g.Status == StatusGarantia.Ativa)
            .ToList()
            .AsReadOnly();

        decimal coberturaTotalBrl = ativas.Sum(g => g.ValorBrl.Valor);
        decimal coberturaLiquidaSemCdbBrl = ativas
            .Where(g => g.Tipo != TipoGarantia.CdbCativo)
            .Sum(g => g.ValorBrl.Valor);

        decimal principalBrl = contrato.Moeda == Moeda.Brl
            ? contrato.ValorPrincipal.Valor
            : 0m;

        decimal percentualCoberturaTotalPct = principalBrl > 0m
            ? Math.Round(coberturaTotalBrl / principalBrl * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        decimal percentualCoberturaLiquidaPct = principalBrl > 0m
            ? Math.Round(coberturaLiquidaSemCdbBrl / principalBrl * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        decimal totalFaturamentoCartaoFracao = await garantiaRepo.GetTotalPercentualFaturamentoCartaoAsync(
            query.ContratoId, cancellationToken);
        decimal percentualFaturamentoCartaoPct = Math.Round(
            totalFaturamentoCartaoFracao * 100m, 2, MidpointRounding.AwayFromZero);

        List<string> alertas = ComputarAlertas(
            contrato, coberturaTotalBrl, percentualCoberturaTotalPct, principalBrl);

        return new IndicadoresGarantiaDto(
            CoberturaTotalBrl: coberturaTotalBrl,
            PercentualCoberturaTotalPct: percentualCoberturaTotalPct,
            CoberturaLiquidaSemCdbBrl: coberturaLiquidaSemCdbBrl,
            PercentualCoberturaLiquidaPct: percentualCoberturaLiquidaPct,
            PercentualFaturamentoCartaoComprometidoPct: percentualFaturamentoCartaoPct,
            Alertas: alertas.AsReadOnly());
    }

    private static List<string> ComputarAlertas(
        Contrato contrato,
        decimal coberturaTotalBrl,
        decimal percentualCoberturaTotalPct,
        decimal principalBrl)
    {
        List<string> alertas = [];

        if (principalBrl > 0m && percentualCoberturaTotalPct < 100m)
        {
            alertas.Add(
                $"Atenção: Cobertura total ({percentualCoberturaTotalPct:F1}%) abaixo de 100% do principal.");
        }

        if (contrato.Moeda != Moeda.Brl)
        {
            alertas.Add(
                "ALERTA CRÍTICO: Operação em moeda estrangeira. Indicadores em BRL podem subestimar exposição real.");
        }

        return alertas;
    }
}

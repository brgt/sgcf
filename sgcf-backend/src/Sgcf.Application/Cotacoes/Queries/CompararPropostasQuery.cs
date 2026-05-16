using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Tabela comparativa de propostas com três métricas: taxa nominal, CET e custo total equivalente.
/// A terceira métrica equaliza propostas com prazos diferentes via CDI. SPEC §5.3, §6.2.
/// </summary>
public sealed record CompararPropostasQuery(Guid CotacaoId) : IRequest<IReadOnlyList<ComparativoDto>>;

public sealed class CompararPropostasQueryHandler(
    ICotacaoRepository cotacaoRepo,
    ICdiSnapshotRepository cdiRepo,
    IClock clock) : IRequestHandler<CompararPropostasQuery, IReadOnlyList<ComparativoDto>>
{
    public async Task<IReadOnlyList<ComparativoDto>> Handle(
        CompararPropostasQuery query,
        CancellationToken cancellationToken)
    {
        Cotacao cotacao = await cotacaoRepo.GetByIdWithPropostasAsync(query.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{query.CotacaoId}' não encontrada.");

        if (cotacao.Propostas.Count == 0)
        {
            return [];
        }

        LocalDate hoje = clock.GetCurrentInstant()
            .InZone(DateTimeZoneProviders.Tzdb["America/Sao_Paulo"]).Date;

        CdiSnapshot? cdiSnapshot = await cdiRepo.GetMaisRecenteAsync(hoje, cancellationToken);
        decimal cdiAa = cdiSnapshot?.CdiAaPercentual ?? 0m;

        List<ComparativoDto> comparativo = new(cotacao.Propostas.Count);

        foreach (Proposta p in cotacao.Propostas)
        {
            decimal taxaNominal = p.TaxaAaPercentual + p.SpreadAaPercentual;
            decimal cet = p.CetCalculadoAaPercentual ?? taxaNominal; // fallback se CET não calculado

            // Custo total equivalente ao prazo da cotação via CDI (SPEC §5.3 coluna 3)
            decimal custoTotalBrl = CalcularCustoTotalEquivalente(
                p, cotacao, cet, cdiAa);

            comparativo.Add(new ComparativoDto(
                p.Id,
                p.BancoId,
                p.MoedaOriginal.ToString(),
                p.PrazoDias,
                Math.Round(taxaNominal, 6, MidpointRounding.AwayFromZero),
                Math.Round(cet, 6, MidpointRounding.AwayFromZero),
                Math.Round(custoTotalBrl, 2, MidpointRounding.AwayFromZero),
                p.ExigeNdf,
                p.GarantiaExigida,
                p.ValorGarantiaExigidaBrl.Valor,
                p.Status.ToString()));
        }

        // Ordena pelo custo total equivalente (menor = melhor — SPEC §5.3)
        comparativo.Sort((a, b) => a.CustoTotalEquivalenteBrl.CompareTo(b.CustoTotalEquivalenteBrl));

        return comparativo.AsReadOnly();
    }

    /// <summary>
    /// Calcula custo total em BRL equalizado para o prazo da cotação.
    /// Usa CDI para descontar/estender fluxo ao prazo-referência da cotação. SPEC §5.3 coluna 3.
    /// </summary>
    private static decimal CalcularCustoTotalEquivalente(
        Proposta proposta,
        Cotacao cotacao,
        decimal cetAaPercentual,
        decimal cdiAaPercentual)
    {
        decimal principalBrl = proposta.MoedaOriginal == Moeda.Brl
            ? proposta.ValorOferecidoMoedaOriginal.Valor
            : Math.Round(proposta.ValorOferecidoMoedaOriginal.Valor * cotacao.PtaxUsadaUsdBrl, 6, MidpointRounding.AwayFromZero);

        // Custo total da proposta para o seu próprio prazo
        decimal custoProposta = principalBrl * cetAaPercentual / 100m * proposta.PrazoDias / 360m;

        // Equaliza para o prazo máximo da cotação via fator CDI
        if (cdiAaPercentual <= 0 || proposta.PrazoDias == cotacao.PrazoMaximoDias)
        {
            return Math.Round(principalBrl + custoProposta, 6, MidpointRounding.AwayFromZero);
        }

        decimal cdiDecimal = cdiAaPercentual / 100m;
        decimal fatorProposta = 1m + cdiDecimal * proposta.PrazoDias / 360m;
        decimal fatorCotacao = 1m + cdiDecimal * cotacao.PrazoMaximoDias / 360m;

        // Valor presente do custo da proposta, re-expandido ao prazo da cotação
        decimal vplCusto = Math.Round(custoProposta / fatorProposta * fatorCotacao, 6, MidpointRounding.AwayFromZero);

        return Math.Round(principalBrl + vplCusto, 6, MidpointRounding.AwayFromZero);
    }
}

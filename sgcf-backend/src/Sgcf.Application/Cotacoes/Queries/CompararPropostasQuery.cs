using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>
/// Tabela comparativa de propostas com três métricas: taxa nominal, CET e custo total equivalente.
/// A terceira métrica equaliza propostas com prazos diferentes via CDI. SPEC §5.3, §6.2.
/// <para>
/// Para cotações Lei 4131, o campo opcional <see cref="AliquotaIrrfPercentual"/> ativa o cálculo
/// informativo de IRRF estimado por proposta. Quando não informado, <c>irrfEstimadoBrl = 0</c>. SPEC §8.3.
/// </para>
/// </summary>
/// <param name="CotacaoId">Identificador da cotação a comparar.</param>
/// <param name="AliquotaIrrfPercentual">
/// Alíquota IRRF em percentual humano (ex: 15 para 15%). Opcional — usado apenas para Lei 4131.
/// Quando null, <c>irrfEstimadoBrl = 0</c> para todas as propostas. SPEC §8.3.
/// </param>
public sealed record CompararPropostasQuery(
    Guid CotacaoId,
    decimal? AliquotaIrrfPercentual = null) : IRequest<IReadOnlyList<ComparativoDto>>;

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

        // PTAX efetiva para cálculo de IRRF: só relevante em cotações cambiais.
        // ! null-forgiving seguro: usado apenas quando MoedaOriginal != Brl (modalidade cambial),
        // cujo invariante de domínio garante PtaxUsadaUsdBrl não-null.
        decimal ptaxParaIrrf = cotacao.PtaxUsadaUsdBrl ?? 1m;

        foreach (Proposta p in cotacao.Propostas)
        {
            decimal taxaNominal = p.TaxaAaPercentual + p.SpreadAaPercentual;
            decimal cet = p.CetCalculadoAaPercentual ?? taxaNominal; // fallback se CET não calculado

            // Custo total equivalente ao prazo da cotação via CDI (SPEC §5.3 coluna 3)
            decimal custoTotalBrl = CalcularCustoTotalEquivalente(
                p, cotacao, cet, cdiAa);

            // IRRF estimado — informativo exclusivo Lei 4131 (SPEC §8.3).
            // Calculado on-demand com a alíquota informada na query. Zero para outras modalidades.
            decimal irrfEstimadoBrl = cotacao.Modalidade == ModalidadeContrato.Lei4131
                ? CalculadoraIrrfEstimado.Calcular(p, ptaxParaIrrf, query.AliquotaIrrfPercentual)
                : 0m;

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
                p.Status.ToString(),
                IrrfEstimadoBrl: irrfEstimadoBrl));
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
        // ! null-forgiving seguro: propostas em moeda não-BRL só existem em cotações cambiais
        // (FINIMP/REFINIMP/Lei4131), cujo invariante de domínio garante PtaxUsadaUsdBrl não-null.
        decimal principalBrl = proposta.MoedaOriginal == Moeda.Brl
            ? proposta.ValorOferecidoMoedaOriginal.Valor
            : Math.Round(proposta.ValorOferecidoMoedaOriginal.Valor * cotacao.PtaxUsadaUsdBrl!.Value, 6, MidpointRounding.AwayFromZero);

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

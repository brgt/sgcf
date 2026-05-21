using NodaTime;

namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Consolidado de efetividade de hedge: exposição cambial total, cobertura via NDF e MtM atual.
/// Retornado no envelope padrão <c>EnvelopeResponse&lt;HedgeEfetividadeDto&gt;</c>.
/// </summary>
public sealed record HedgeEfetividadeDto(
    IReadOnlyList<HedgeEfetividadeMoedaDto> PorMoeda,
    decimal ExposicaoTotalBrl,
    decimal CoberturaHedgeBrl,
    decimal TaxaCoberturaPct,
    decimal AjusteMtmTotalBrl);

/// <summary>
/// Exposição e cobertura de hedge agrupadas por moeda base dos contratos.
/// </summary>
public sealed record HedgeEfetividadeMoedaDto(
    string Moeda,
    decimal ExposicaoBrl,
    decimal CoberturaHedgeBrl,
    decimal TaxaCoberturaPct,
    decimal MtmBrl,
    IReadOnlyList<HedgeInstrumentoResumoDto> Instrumentos);

/// <summary>
/// Resumo de um instrumento de hedge individual com nocional e MtM correntes.
/// </summary>
public sealed record HedgeInstrumentoResumoDto(
    Guid Id,
    string Tipo,
    decimal NocionalBrl,
    decimal MtmBrl,
    LocalDate DataVencimento);

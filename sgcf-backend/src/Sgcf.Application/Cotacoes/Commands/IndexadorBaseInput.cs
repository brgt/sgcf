namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Entrada de API do indexador base (objeto aninhado em POST/PATCH). <see cref="Tipo"/> é string
/// (nome do enum TipoIndexador). Mapeado para o VO de domínio no handler. SPEC S40 §2.4.
/// </summary>
public sealed record IndexadorBaseInput(
    string? Tipo = null,
    decimal? PercentualCdi = null,
    decimal? SpreadAa = null,
    decimal? TaxaPrefixadaAa = null);

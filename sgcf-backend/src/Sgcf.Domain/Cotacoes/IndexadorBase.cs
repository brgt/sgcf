namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Indexador base pretendido para a cotação (intenção de base de juros).
/// Modelado em colunas planas na persistência; serializado como objeto aninhado na API.
/// A coerência tipo↔campo numérico é validação SUAVE (não lança): use <see cref="EhCoerente"/>
/// para emitir alerta sem bloquear. SPEC S40 §2.4 e §4.5.
/// </summary>
public sealed record IndexadorBase
{
    /// <summary>Tipo de indexador. Opcional; quando ausente, o objeto é considerado vazio/parcial.</summary>
    public TipoIndexador? Tipo { get; init; }

    /// <summary>Percentual do CDI (ex.: 112.5 = 112,5% do CDI). Aplicável a <see cref="TipoIndexador.CdiPercentual"/>.</summary>
    public decimal? PercentualCdi { get; init; }

    /// <summary>Spread em pontos percentuais ao ano. Aplicável a CdiMaisSpread, Sofr, Euribor, Tlp e Ipca.</summary>
    public decimal? SpreadAa { get; init; }

    /// <summary>Taxa prefixada em pontos percentuais ao ano. Aplicável a <see cref="TipoIndexador.Prefixado"/>.</summary>
    public decimal? TaxaPrefixadaAa { get; init; }

    /// <summary>
    /// Verdadeiro quando o campo numérico coerente com <see cref="Tipo"/> está presente.
    /// Quando <see cref="Tipo"/> é null, considera-se coerente (objeto ausente ou parcial).
    /// Selic não exige campo numérico específico.
    /// </summary>
    public bool EhCoerente() => Tipo switch
    {
        null => true,
        TipoIndexador.CdiPercentual => PercentualCdi is not null,
        TipoIndexador.Prefixado => TaxaPrefixadaAa is not null,
        TipoIndexador.CdiMaisSpread
            or TipoIndexador.Sofr
            or TipoIndexador.Euribor
            or TipoIndexador.Tlp
            or TipoIndexador.Ipca => SpreadAa is not null,
        TipoIndexador.Selic => true,
        _ => true,
    };
}

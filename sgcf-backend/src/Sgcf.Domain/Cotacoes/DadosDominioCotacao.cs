namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Dados de domínio opcionais da cotação (intenção do operador), agrupados para evitar
/// um factory com excesso de parâmetros. Cada campo é normalizado por modalidade na
/// criação (campos não aplicáveis são ignorados). SPEC S40 §2.3–§2.5.
/// </summary>
/// <param name="CarenciaMeses">Carência pretendida em meses. Aplicável a Lei4131, Nce, CapitalDeGiro e Fgi.</param>
/// <param name="IndexadorBase">Base de juros pretendida. Aplicável a todas as modalidades.</param>
/// <param name="FinalidadeBndes">Finalidade/enquadramento BNDES pretendido. Aplicável apenas a Fgi.</param>
/// <param name="BancoRepassadorPretendido">Banco repassador pretendido. Aplicável apenas a Fgi.</param>
/// <param name="PercentualCoberturaFgi">Percentual de cobertura FGI pretendido (0..100). Aplicável apenas a Fgi.</param>
public sealed record DadosDominioCotacao(
    int? CarenciaMeses = null,
    IndexadorBase? IndexadorBase = null,
    string? FinalidadeBndes = null,
    string? BancoRepassadorPretendido = null,
    decimal? PercentualCoberturaFgi = null)
{
    /// <summary>Instância sem nenhum dado de domínio (usada pelo caminho legado).</summary>
    public static DadosDominioCotacao Vazio { get; } = new();
}

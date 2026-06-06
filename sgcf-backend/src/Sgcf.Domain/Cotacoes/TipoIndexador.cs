namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Base de juros pretendida pela cotação (intenção). Habilita comparação de CET
/// em bases comparáveis entre propostas. Sujeito a validação de produto.
/// Valores fixos — não reordenar (persistência por nome via converter). SPEC S40 §2.4.
/// </summary>
public enum TipoIndexador
{
    CdiPercentual = 1,
    CdiMaisSpread = 2,
    Prefixado = 3,
    Tlp = 4,
    Ipca = 5,
    Selic = 6,
    Sofr = 7,
    Euribor = 8,
}

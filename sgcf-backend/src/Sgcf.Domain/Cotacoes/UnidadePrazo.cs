namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Unidade de medida do prazo máximo da cotação (intenção do operador).
/// Valores fixos — não reordenar (persistência por nome via converter).
/// O campo canônico <c>PrazoMaximoDias</c> é derivado da dupla {valor, unidade}.
/// SPEC S40 §2.1.
/// </summary>
public enum UnidadePrazo
{
    Dias = 1,
    Meses = 2,
}

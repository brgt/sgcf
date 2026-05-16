namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Ciclo de vida de uma Cotacao. Valores byte fixos — não reordenar (compatibilidade com migrations).
/// Ver máquina de estados em docs/specs/cotacoes/SPEC.md §4.
/// </summary>
public enum StatusCotacao : byte
{
    Rascunho    = 1,
    EmCaptacao  = 2,
    Comparada   = 3,
    Aceita      = 4,
    Convertida  = 5,
    Recusada    = 6,
}

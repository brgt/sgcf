namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Ciclo de vida de uma Proposta. Valores byte fixos — não reordenar (compatibilidade com migrations).
/// </summary>
public enum StatusProposta : byte
{
    Recebida  = 1,
    Aceita    = 2,
    Recusada  = 3,
    Expirada  = 4,
}

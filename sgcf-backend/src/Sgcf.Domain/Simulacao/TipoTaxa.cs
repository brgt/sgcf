namespace Sgcf.Domain.Simulacao;

/// <summary>
/// Forma de indexação da taxa de juros de uma <see cref="SimulacaoContratacao"/>.
/// SPEC §6.3 — invariantes I-6 e I-7.
/// </summary>
public enum TipoTaxa : byte
{
    /// <summary>
    /// Taxa nominal anual fixa. Campo <c>TaxaAa</c> obrigatório; <c>SpreadAa</c> deve ser nulo.
    /// </summary>
    Fixa = 1,

    /// <summary>
    /// CDI + spread anual fixo. Campo <c>SpreadAa</c> obrigatório; <c>TaxaAa</c> deve ser nulo.
    /// Válido apenas para <c>Moeda == Brl</c>.
    /// </summary>
    CdiSpread = 2
}

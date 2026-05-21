namespace Sgcf.Domain.Tesouraria;

/// <summary>
/// Classifica a direção financeira de um evento de fluxo de caixa.
/// </summary>
public enum TipoEventoFluxo
{
    /// <summary>Evento que representa entrada de recursos no caixa.</summary>
    Entrada = 1,

    /// <summary>Evento que representa saída de recursos do caixa.</summary>
    Saida = 2
}

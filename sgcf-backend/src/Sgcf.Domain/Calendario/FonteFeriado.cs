namespace Sgcf.Domain.Calendario;

/// <summary>
/// Origem (fonte de dados) do registro do feriado.
/// </summary>
public enum FonteFeriado : byte
{
    /// <summary>ANBIMA — calendário oficial do mercado financeiro brasileiro.</summary>
    Anbima = 1,

    /// <summary>B3 — calendário de pregão da bolsa.</summary>
    B3 = 2,

    /// <summary>BACEN — calendário bancário.</summary>
    Bacen = 3,

    /// <summary>Inserido manualmente por operador (seed ou upload).</summary>
    Manual = 4
}

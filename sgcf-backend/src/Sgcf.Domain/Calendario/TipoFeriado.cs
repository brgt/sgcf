namespace Sgcf.Domain.Calendario;

/// <summary>
/// Classificação do feriado conforme a esfera que o reconhece para fins de
/// fechamento de mercado financeiro / bancário no Brasil.
/// </summary>
public enum TipoFeriado : byte
{
    /// <summary>Feriado civil nacional (Lei 662/49 e correlatas).</summary>
    Nacional = 1,

    /// <summary>Feriado bancário (Resolução BACEN / portarias).</summary>
    Bancario = 2,

    /// <summary>Feriado de pregão da B3.</summary>
    BolsaB3 = 3,

    /// <summary>Feriado regional (estadual ou municipal).</summary>
    Regional = 4
}

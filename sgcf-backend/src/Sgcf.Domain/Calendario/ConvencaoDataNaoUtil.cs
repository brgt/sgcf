namespace Sgcf.Domain.Calendario;

/// <summary>
/// Define como vencimentos que caem em dias não úteis devem ser deslocados.
/// A convenção correta para cada modalidade é determinada pelo contrato/regulamentação aplicável.
/// </summary>
public enum ConvencaoDataNaoUtil : byte
{
    /// <summary>Desloca para o próximo dia útil.</summary>
    FollowingBusinessDay = 1,

    /// <summary>
    /// Desloca para o próximo dia útil, exceto quando cruzar para o mês seguinte —
    /// nesse caso desloca para o dia útil anterior (convenção ISDA padrão).
    /// </summary>
    ModifiedFollowing = 2,

    /// <summary>Desloca para o dia útil imediatamente anterior.</summary>
    PrecedingBusinessDay = 3,

    /// <summary>Mantém a data original, independentemente de ser dia útil.</summary>
    Nenhuma = 4
}

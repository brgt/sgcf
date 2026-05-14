namespace Sgcf.Domain.Calendario;

/// <summary>
/// Convenção de mercado para ajuste de datas que recaem em dia não útil
/// (sábado, domingo ou feriado). Padrão internacional ISDA 2006 §4.12.
/// </summary>
public enum ConvencaoDataNaoUtil : byte
{
    /// <summary>Não ajusta — mantém a data nominal mesmo em dia não útil.</summary>
    Unadjusted = 0,

    /// <summary>
    /// Próximo dia útil. Padrão de mercado para empréstimos no Brasil
    /// (FINIMP, FGI, NCE, Balcão).
    /// </summary>
    Following = 1,

    /// <summary>
    /// Próximo dia útil, exceto se ultrapassar o mês — nesse caso, recua
    /// para o dia útil anterior. Usado em derivativos e swaps atrelados
    /// a mês de competência.
    /// </summary>
    ModifiedFollowing = 2,

    /// <summary>Dia útil anterior. Pouco usado no Brasil.</summary>
    Preceding = 3,

    /// <summary>
    /// Dia útil anterior, exceto se recuar para o mês anterior — nesse caso,
    /// avança para o próximo dia útil.
    /// </summary>
    ModifiedPreceding = 4
}

namespace Sgcf.Domain.Calendario;

/// <summary>
/// Abrangência geográfica do feriado.
/// Para o motor de cronograma, apenas <see cref="Brasil"/> é considerado por padrão;
/// escopos regionais existem apenas para fins informativos.
/// </summary>
public enum EscopoFeriado : byte
{
    /// <summary>Feriado de abrangência nacional.</summary>
    Brasil = 1,

    /// <summary>Estado de São Paulo (UF SP).</summary>
    SaoPaulo = 2,

    /// <summary>Estado do Rio de Janeiro (UF RJ).</summary>
    RioDeJaneiro = 3,

    /// <summary>Município (uso genérico — detalhamento via descrição).</summary>
    Municipal = 4
}

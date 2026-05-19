namespace Sgcf.Domain.Simulacao;

/// <summary>
/// Ciclo de vida do <see cref="CenarioSimulacao"/>.
/// Transições permitidas: Rascunho → Ativo → Arquivado.
/// SPEC §6.2.
/// </summary>
public enum StatusCenarioSimulacao : byte
{
    /// <summary>Em elaboração. Aceita todas as operações de edição.</summary>
    Rascunho = 1,

    /// <summary>Aprovado para uso. Ainda aceita refinamentos de simulações.</summary>
    Ativo = 2,

    /// <summary>Encerrado. Imutável — consulta apenas para auditoria.</summary>
    Arquivado = 3
}

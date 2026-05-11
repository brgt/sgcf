using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao;

/// <summary>
/// Resultado completo de uma simulação de antecipação para um padrão específico.
/// </summary>
public sealed record ResultadoSimulacaoAntecipacao(
    PadraoAntecipacao Padrao,
    bool Permitido,
    IReadOnlyList<string> Alertas,
    IReadOnlyList<ComponenteCusto> Componentes,
    Money TotalAQuitar,
    bool ExigeAnuenciaExpressa
);

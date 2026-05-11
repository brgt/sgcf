namespace Sgcf.Application.Antecipacao;

public sealed record ComponenteCustoDto(
    string Codigo,
    string Descricao,
    decimal ValorMoedaOriginal,
    string Sinal);

/// <summary>
/// Comparativo entre antecipar agora versus manter o contrato até o vencimento.
/// </summary>
public sealed record ComparativoSimulacaoDto(
    decimal CustoSeNaoAnteciparMoedaOriginal,
    decimal DiferencaMoedaOriginal,
    string DecisaoOtima,
    string Justificativa);

public sealed record ResultadoSimulacaoDto(
    Guid? SimulacaoId,
    string PadraoAplicado,
    bool Permitido,
    IReadOnlyList<string> Alertas,
    IReadOnlyList<ComponenteCustoDto> ComponentesCusto,
    decimal TotalAPagarMoedaOriginal,
    decimal TotalAPagarBrl,
    decimal? CotacaoAplicada,
    ComparativoSimulacaoDto? Comparativo);

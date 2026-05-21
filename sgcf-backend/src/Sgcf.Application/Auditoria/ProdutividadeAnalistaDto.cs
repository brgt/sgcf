namespace Sgcf.Application.Auditoria;

/// <summary>
/// Agregação de produtividade de um analista para um período, calculada a partir do AuditLog.
/// </summary>
/// <param name="ActorSub">Identificador do ator (sub do JWT).</param>
/// <param name="ActorRole">Papel/perfil do ator no momento das operações.</param>
/// <param name="TotalOperacoes">Total de operações registradas no período.</param>
/// <param name="SlaMediaMinutos">
/// Tempo médio (em minutos) entre a primeira e a última operação sobre uma mesma entidade,
/// calculado apenas para entidades com duas ou mais operações no período.
/// <c>null</c> quando não há entidades com múltiplas operações.
/// </param>
/// <param name="PorEntidade">Distribuição de operações por tipo de entidade.</param>
public sealed record ProdutividadeAnalistaDto(
    string ActorSub,
    string ActorRole,
    int TotalOperacoes,
    double? SlaMediaMinutos,
    IReadOnlyList<ProdutividadePorEntidadeDto> PorEntidade);

/// <summary>
/// Contagem de operações para um tipo de entidade específico.
/// </summary>
/// <param name="Entidade">Nome da entidade (ex: "Contrato", "Cotacao").</param>
/// <param name="Operacoes">Total de operações sobre esse tipo de entidade no período.</param>
public sealed record ProdutividadePorEntidadeDto(
    string Entidade,
    int Operacoes);

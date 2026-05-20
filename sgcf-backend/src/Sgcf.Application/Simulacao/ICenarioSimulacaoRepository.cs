using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao;

/// <summary>
/// Contrato de persistência para o agregado <see cref="CenarioSimulacao"/>.
/// A implementação vive em <c>Sgcf.Infrastructure</c> (Task 2.2).
///
/// Convenção de Unit-of-Work: <see cref="Add"/>, <see cref="Update"/> e <see cref="Remove"/>
/// não persistem imediatamente — chamam <see cref="SaveChangesAsync"/> para confirmar.
/// </summary>
public interface ICenarioSimulacaoRepository
{
    /// <summary>Registra um novo cenário para inserção.</summary>
    public void Add(CenarioSimulacao cenario);

    /// <summary>Marca um cenário existente para atualização.</summary>
    public void Update(CenarioSimulacao cenario);

    /// <summary>Marca um cenário para remoção (hard delete — use <see cref="CenarioSimulacao.Deletar"/> para soft delete).</summary>
    public void Remove(CenarioSimulacao cenario);

    /// <summary>Retorna o cenário pelo Id, incluindo todas as simulações filhas. Null se não encontrado (ou soft-deletado).</summary>
    public Task<CenarioSimulacao?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retorna múltiplos cenários em uma única consulta ao banco, preservando a ordem de
    /// <paramref name="ids"/>. Cenários não encontrados ou soft-deletados são omitidos do resultado.
    ///
    /// Use no lugar de múltiplos <see cref="GetByIdAsync"/> em loop — elimina o N+1 query.
    /// </summary>
    public Task<IReadOnlyList<CenarioSimulacao>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Lista cenários com filtros opcionais. Exclui soft-deletados por padrão (query filter).
    /// </summary>
    /// <param name="status">Filtra por status. Null retorna todos os status.</param>
    /// <param name="anoBase">Filtra por ano-calendário de referência.</param>
    /// <param name="criadoPor">Filtra pelo identificador do criador (sub do JWT).</param>
    public Task<IReadOnlyList<CenarioSimulacao>> ListAsync(
        StatusCenarioSimulacao? status,
        int? anoBase,
        string? criadoPor,
        CancellationToken ct = default);

    /// <summary>Persiste todas as alterações pendentes na unit-of-work.</summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}

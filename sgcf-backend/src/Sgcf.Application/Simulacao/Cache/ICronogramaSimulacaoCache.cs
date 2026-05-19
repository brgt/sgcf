using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Simulacao.Cache;

/// <summary>
/// Porta de cache para o cronograma hipotético de uma <see cref="Sgcf.Domain.Simulacao.SimulacaoContratacao"/>.
///
/// <para>
/// <b>AD-3 (chave de cache):</b> <c>sim:cronograma:{cenarioId}:{simulacaoId}:v{version}</c>.
/// O campo <c>Version</c> da entidade é incrementado a cada mutação, garantindo que
/// versões antigas do cache expirem automaticamente sem necessidade de invalidação
/// explícita na maioria dos fluxos de leitura.
/// </para>
///
/// <para>
/// Para mutações que precisam invalidar imediatamente todas as versões anteriores
/// (ex: delete, update com chave desconhecida), use <see cref="InvalidarPorSimulacaoAsync"/>.
/// </para>
/// </summary>
public interface ICronogramaSimulacaoCache
{
    /// <summary>
    /// Retorna o cronograma em cache para a versão indicada,
    /// ou <see langword="null"/> se não houver entrada válida.
    /// </summary>
    public Task<IReadOnlyList<EventoCronogramaGerado>?> GetAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste o cronograma no cache com TTL configurado (padrão 60 s).
    /// Registra a chave no índice auxiliar de invalidação por simulação.
    /// </summary>
    public Task SetAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        IReadOnlyList<EventoCronogramaGerado> cronograma,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida todas as versões de uma simulação específica.
    /// Útil em mutações (update/delete) onde o caller não conhece a <c>Version</c> atual.
    /// Usa o índice auxiliar registrado por <see cref="SetAsync"/> para localizar as chaves.
    /// </summary>
    public Task InvalidarPorSimulacaoAsync(
        Guid cenarioId,
        Guid simulacaoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenta obter o cronograma em cache; em caso de miss, executa <paramref name="factory"/>,
    /// armazena o resultado e o retorna. Nunca retorna null.
    /// </summary>
    /// <param name="factory">
    /// Delegate assíncrono que calcula o cronograma quando não há entrada em cache.
    /// Executado no máximo uma vez por (cenarioId, simulacaoId, version).
    /// </param>
    public Task<IReadOnlyList<EventoCronogramaGerado>> GetOrCreateAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        Func<Task<IReadOnlyList<EventoCronogramaGerado>>> factory,
        CancellationToken cancellationToken = default);
}

using Sgcf.Application.Simulacao.Cache;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Infrastructure.Cache.Simulacao;

/// <summary>
/// Implementação no-op de <see cref="ICronogramaSimulacaoCache"/>.
///
/// Usada quando Redis não está configurado no ambiente (ex: desenvolvimento local sem Docker,
/// testes de integração sem Testcontainers Redis). Todos os métodos de leitura retornam
/// miss imediato; o caller sempre recalcula o cronograma on-the-fly.
///
/// Registrada via DI como fallback explícito em <see cref="Sgcf.Infrastructure.DependencyInjection"/>
/// — substitui o padrão Service Locator anterior que usava <c>IServiceProvider.GetService</c>.
/// </summary>
public sealed class NullCronogramaSimulacaoCache : ICronogramaSimulacaoCache
{
    /// <inheritdoc/>
    /// <remarks>Sempre retorna null — força o caller a recalcular o cronograma.</remarks>
    public Task<IReadOnlyList<EventoCronogramaGerado>?> GetAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EventoCronogramaGerado>?>(null);

    /// <inheritdoc/>
    /// <remarks>No-op — sem Redis não há onde persistir.</remarks>
    public Task SetAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        IReadOnlyList<EventoCronogramaGerado> cronograma,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>No-op — sem cache não há o que invalidar.</remarks>
    public Task InvalidarPorSimulacaoAsync(
        Guid cenarioId,
        Guid simulacaoId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>Sempre chama <paramref name="factory"/> — sem cache, sempre recalcula.</remarks>
    public Task<IReadOnlyList<EventoCronogramaGerado>> GetOrCreateAsync(
        Guid cenarioId,
        Guid simulacaoId,
        int version,
        Func<Task<IReadOnlyList<EventoCronogramaGerado>>> factory,
        CancellationToken cancellationToken = default)
        => factory();
}

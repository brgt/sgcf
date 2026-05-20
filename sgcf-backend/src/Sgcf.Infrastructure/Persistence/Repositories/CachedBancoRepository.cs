using Microsoft.Extensions.Caching.Memory;
using Sgcf.Application.Bancos;
using Sgcf.Domain.Bancos;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Decorator de <see cref="IBancoRepository"/> que adiciona cache in-memory com TTL de 10 minutos
/// para <see cref="GetByIdAsync"/>.
///
/// <para>
/// <b>Motivação:</b> <c>GetByIdAsync</c> é chamado em múltiplos handlers de leitura intensiva
/// (QuadroDivida, Simulação, Calendário) para resolver <c>bancoId → Banco</c>. Bancos mudam raramente
/// — o custo de um round-trip ao banco de dados a cada chamada não agrega valor.
/// </para>
///
/// <para>
/// <b>Invalidação:</b> mutações que alteram o estado de um <see cref="Banco"/> devem chamar
/// <see cref="InvalidarCacheBanco"/> após persistir, ou confiar que o TTL de 10 minutos
/// é aceitável para o domínio. Os handlers <c>UpdateBancoConfigCommandHandler</c> e
/// <c>CreateBancoCommandHandler</c> não precisam de invalidação explícita — o primeiro
/// atualiza um banco existente cujo estado muda (invalida), o segundo cria um novo banco
/// não presente no cache (nenhum impacto em cache miss).
/// </para>
///
/// <para>
/// <b>Registro:</b> registrado via composição em <see cref="Sgcf.Infrastructure.DependencyInjection"/>.
/// O inner <c>BancoRepository</c> é registrado diretamente; este decorator envolve o inner.
/// </para>
/// </summary>
internal sealed class CachedBancoRepository(BancoRepository inner, IMemoryCache cache) : IBancoRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private static string ChaveBanco(Guid id) => $"banco:{id:N}";

    /// <inheritdoc/>
    /// <remarks>Cache hit: retorna imediatamente sem I/O. Cache miss: delega ao inner e armazena o resultado.</remarks>
    public Task<Banco?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(ChaveBanco(id), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return inner.GetByIdAsync(id, ct);
        })!;

    /// <inheritdoc/>
    /// <remarks>Remove a entrada de cache — garante que a próxima leitura reflita o estado persistido.</remarks>
    public void InvalidarCache(Guid id) =>
        cache.Remove(ChaveBanco(id));

    // ── Operações de escrita e listagem — delegadas ao inner sem cache ─────────
    // Listagens não são cacheadas: variam por filtro e têm padrão de acesso distinto.

    /// <inheritdoc/>
    public Task<Banco?> GetByCodigoCompeAsync(string codigoCompe, CancellationToken ct = default) =>
        inner.GetByCodigoCompeAsync(codigoCompe, ct);

    /// <inheritdoc/>
    public Task<Banco?> GetByApelidoAsync(string apelido, CancellationToken ct = default) =>
        inner.GetByApelidoAsync(apelido, ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<Banco>> ListAllAsync(CancellationToken ct = default) =>
        inner.ListAllAsync(ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<Banco>> ListFilteredAsync(string? search, CancellationToken ct = default) =>
        inner.ListFilteredAsync(search, ct);

    /// <inheritdoc/>
    public void Add(Banco banco) => inner.Add(banco);

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<Banco>> ListComLimiteCreditoSetadoAsync(CancellationToken ct = default) =>
        inner.ListComLimiteCreditoSetadoAsync(ct);
}

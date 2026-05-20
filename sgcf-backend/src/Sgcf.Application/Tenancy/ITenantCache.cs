using Sgcf.Domain.Tenancy;

namespace Sgcf.Application.Tenancy;

/// <summary>
/// Cache de tenant com dois níveis: MemoryCache local (5 min TTL) + invalidação via Redis pub/sub.
/// </summary>
public interface ITenantCache
{
    /// <summary>Retorna info de tenant por ID, ou null se não estiver em cache.</summary>
    public Task<TenantInfo?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Retorna info de tenant por slug, ou null se não estiver em cache.</summary>
    public Task<TenantInfo?> GetBySlugAsync(string slug, CancellationToken ct);

    /// <summary>Armazena a info do tenant no cache local e publica no Redis para replicar.</summary>
    public Task SetAsync(TenantInfo info, CancellationToken ct);

    /// <summary>
    /// Invalida localmente o tenant e publica no canal Redis de invalidação
    /// para que todas as instâncias invalidem sua cópia local.
    /// </summary>
    public Task InvalidateAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Projeção imutável de <see cref="Sgcf.Domain.Tenancy.Tenant"/> para o cache —
/// contém apenas o que o middleware precisa para resolver o contexto.
/// </summary>
public sealed record TenantInfo(Guid Id, string Slug, StatusTenant Status);

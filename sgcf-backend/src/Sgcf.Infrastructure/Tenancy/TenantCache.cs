using Microsoft.Extensions.Caching.Memory;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Tenancy;
using StackExchange.Redis;

namespace Sgcf.Infrastructure.Tenancy;

/// <summary>
/// Cache de tenant em dois níveis: MemoryCache local (5 min TTL) + invalidação cross-instância via Redis pub/sub.
/// Redis pub/sub é usado apenas para invalidação — os dados residem no MemoryCache de cada pod.
/// Quando Redis não está disponível (<paramref name="redis"/> é null), a invalidação é apenas local.
/// </summary>
internal sealed class TenantCache(IMemoryCache memoryCache, IConnectionMultiplexer? redis = null) : ITenantCache
{
    /// <summary>Canal Redis para broadcast de invalidação de tenant entre instâncias.</summary>
    public const string InvalidationChannel = "sgcf:tenant:invalidate";

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    // Chaves de cache
    private static string KeyById(Guid id) => $"tenant:id:{id}";
    private static string KeyBySlug(string slug) => $"tenant:slug:{slug}";

    public Task<TenantInfo?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        TenantInfo? info = memoryCache.Get<TenantInfo>(KeyById(id));
        return Task.FromResult(info);
    }

    public Task<TenantInfo?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        TenantInfo? info = memoryCache.Get<TenantInfo>(KeyBySlug(slug));
        return Task.FromResult(info);
    }

    public Task SetAsync(TenantInfo info, CancellationToken ct)
    {
        MemoryCacheEntryOptions opts = new MemoryCacheEntryOptions().SetAbsoluteExpiration(Ttl);
        memoryCache.Set(KeyById(info.Id), info, opts);
        memoryCache.Set(KeyBySlug(info.Slug), info, opts);
        return Task.CompletedTask;
    }

    public async Task InvalidateAsync(Guid id, CancellationToken ct)
    {
        InvalidateLocal(id);

        if (redis is null)
        {
            return;
        }

        // Publica o ID no canal Redis para que outras instâncias invalidem sua cópia local.
        ISubscriber sub = redis.GetSubscriber();
        await sub.PublishAsync(
            RedisChannel.Literal(InvalidationChannel),
            id.ToString(),
            CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Invalida a entrada local do tenant. Chamado tanto pelo <see cref="InvalidateAsync"/>
    /// quanto pelo <c>TenantCacheInvalidationSubscriber</c> ao receber mensagem do Redis.
    /// </summary>
    internal void InvalidateLocal(Guid id)
    {
        // Precisa do slug para remover a segunda chave; tenta do cache antes de desistir.
        TenantInfo? info = memoryCache.Get<TenantInfo>(KeyById(id));
        memoryCache.Remove(KeyById(id));

        if (info is not null)
        {
            memoryCache.Remove(KeyBySlug(info.Slug));
        }
    }
}

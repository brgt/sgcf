using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Sgcf.Infrastructure.Tenancy;

/// <summary>
/// Background service que assina o canal Redis <c>sgcf:tenant:invalidate</c> e
/// invalida o MemoryCache local quando outra instância publica uma invalidação.
/// Isso garante consistência de cache entre pods em ambiente multi-instância.
/// </summary>
internal sealed partial class TenantCacheInvalidationSubscriber(
    TenantCache cache,
    IConnectionMultiplexer redis,
    ILogger<TenantCacheInvalidationSubscriber> logger)
    : IHostedService
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Invalidação de cache de tenant recebida via Redis para ID {TenantId}.")]
    private static partial void LogInvalidacaoRecebida(ILogger logger, string tenantId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Mensagem de invalidação de tenant contém ID inválido: '{Valor}'.")]
    private static partial void LogIdInvalido(ILogger logger, string valor);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ISubscriber sub = redis.GetSubscriber();
        await sub.SubscribeAsync(
            RedisChannel.Literal(TenantCache.InvalidationChannel),
            OnMessageReceived);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnMessageReceived(RedisChannel channel, RedisValue message)
    {
        string? raw = message.ToString();
        if (raw is null || !Guid.TryParse(raw, out Guid tenantId))
        {
            LogIdInvalido(logger, raw ?? "(null)");
            return;
        }

        LogInvalidacaoRecebida(logger, raw);
        cache.InvalidateLocal(tenantId);
    }
}

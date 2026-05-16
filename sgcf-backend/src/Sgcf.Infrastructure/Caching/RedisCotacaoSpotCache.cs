using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;
using System.Globalization;

namespace Sgcf.Infrastructure.Caching;

internal sealed partial class RedisCotacaoSpotCache(
    IDistributedCache cache,
    ICotacaoFxRepository cotacaoRepo,
    IClock clock,
    ILogger<RedisCotacaoSpotCache> logger) : ICotacaoSpotCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit para spot {Moeda}.")]
    private static partial void LogCacheHit(ILogger logger, Moeda moeda);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache miss para spot {Moeda}, consultando DB.")]
    private static partial void LogCacheMiss(ILogger logger, Moeda moeda);

    public async Task<Money?> GetSpotAsync(Moeda moeda, CancellationToken cancellationToken)
    {
        string key = string.Create(CultureInfo.InvariantCulture, $"sgcf:spot:{moeda}");
        string? cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null && decimal.TryParse(cached, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal cachedRate))
        {
            LogCacheHit(logger, moeda);
            return new Money(cachedRate, Moeda.Brl);
        }

        LogCacheMiss(logger, moeda);
        LocalDate dataRef = clock.GetCurrentInstant().InUtc().Date;
        CotacaoFx? cotacao = await cotacaoRepo.GetMaisRecenteAsync(moeda, TipoCotacao.SpotIntraday, dataRef, cancellationToken);
        if (cotacao is null)
        {
            return null;
        }

        decimal midRate = Math.Round((cotacao.ValorCompra.Valor + cotacao.ValorVenda.Valor) / 2m, 6, MidpointRounding.AwayFromZero);
        await cache.SetStringAsync(
            key,
            midRate.ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl },
            cancellationToken);

        return new Money(midRate, Moeda.Brl);
    }
}

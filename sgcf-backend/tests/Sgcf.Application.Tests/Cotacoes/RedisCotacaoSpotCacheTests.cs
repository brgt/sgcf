using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Sgcf.Infrastructure.Caching;
using System.Text;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Domain")]
public sealed class RedisCotacaoSpotCacheTests
{
    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 11, 12, 0));
        return clock;
    }

    private static CotacaoFx CriarCotacaoFx(decimal compra, decimal venda)
    {
        return CotacaoFx.Criar(
            Moeda.Usd,
            TipoCotacao.SpotIntraday,
            new Money(compra, Moeda.Brl),
            new Money(venda, Moeda.Brl),
            "BCB_OLINDA",
            Instant.FromUtc(2026, 5, 11, 12, 0, 0));
    }

    // ── Teste 1: Cache hit → retorna valor sem consultar DB ───────────────────

    [Fact]
    public async Task GetSpotAsync_CacheHit_RetornaSemConsultarDb()
    {
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();
        ILogger<RedisCotacaoSpotCache> logger = NullLogger<RedisCotacaoSpotCache>.Instance;

        // GetStringAsync (extension) calls GetAsync(key, ct) internally — mock that
        byte[] bytes = Encoding.UTF8.GetBytes("5.30");
        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(bytes));

        RedisCotacaoSpotCache sut = new(cache, cotacaoRepo, clock, logger);

        Money? resultado = await sut.GetSpotAsync(Moeda.Usd, CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Value.Valor.Should().Be(5.30m);
        resultado.Value.Moeda.Should().Be(Moeda.Brl);

        // DB must NOT have been queried
        await cotacaoRepo.DidNotReceive().GetMaisRecenteAsync(
            Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>());
    }

    // ── Teste 2: Cache miss + DB tem resultado → popula cache e retorna ───────

    [Fact]
    public async Task GetSpotAsync_CacheMiss_ConsultaDbEPopulaCache()
    {
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();
        ILogger<RedisCotacaoSpotCache> logger = NullLogger<RedisCotacaoSpotCache>.Instance;

        // Cache miss: GetAsync returns null bytes
        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));

        CotacaoFx cotacaoFx = CriarCotacaoFx(5.28m, 5.32m); // mid = 5.30
        cotacaoRepo.GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.SpotIntraday, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(cotacaoFx);

        RedisCotacaoSpotCache sut = new(cache, cotacaoRepo, clock, logger);

        Money? resultado = await sut.GetSpotAsync(Moeda.Usd, CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Value.Valor.Should().Be(5.30m);

        // Cache must have been populated via SetStringAsync → SetAsync
        await cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.Contains("Usd")),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    // ── Teste 3: Cache miss + DB vazio → retorna null, não popula cache ───────

    [Fact]
    public async Task GetSpotAsync_CacheMissEDbVazio_RetornaNull()
    {
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();
        ILogger<RedisCotacaoSpotCache> logger = NullLogger<RedisCotacaoSpotCache>.Instance;

        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));

        cotacaoRepo.GetMaisRecenteAsync(
            Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null);

        RedisCotacaoSpotCache sut = new(cache, cotacaoRepo, clock, logger);

        Money? resultado = await sut.GetSpotAsync(Moeda.Usd, CancellationToken.None);

        resultado.Should().BeNull();

        // Cache must NOT have been written
        await cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }
}

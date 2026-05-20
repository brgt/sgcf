using FluentAssertions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Infrastructure.Caching;

using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Prove-It: RedisCotacaoSpotCache.GetSpotAsync deve usar fuso BRT ao montar dataRef
/// para a consulta de fallback ao repositório.
///
/// Cenário: 2026-05-19 23:30 BRT == 2026-05-20 02:30 UTC.
/// Com InUtc(), a query buscaria cotações do dia 2026-05-20 — data futura no BR.
/// Com InZone(BRT), busca o dia 2026-05-19 — correto para o pregão em andamento.
/// </summary>
[Trait("Category", "Domain")]
public sealed class RedisCotacaoSpotCacheTimezoneTests
{
    private static readonly DateTimeZone FusoBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // 2026-05-19 23:30 BRT == 2026-05-20 02:30 UTC
    private static readonly Instant InstandeViraNoiteBrt = Instant.FromUtc(2026, 5, 20, 2, 30);

    [Fact]
    public async Task GetSpotAsync_CacheMiss_As2330Brt_ConsultaDbComDataBrt_2026_05_19()
    {
        // Arrange — cache miss força consulta ao repositório
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null)); // cache miss

        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoRepo.GetMaisRecenteAsync(
                Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null); // resultado irrelevante para este teste

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        RedisCotacaoSpotCache sut = new(cache, cotacaoRepo, clock, NullLogger<RedisCotacaoSpotCache>.Instance);

        // Act
        await sut.GetSpotAsync(Moeda.Usd, CancellationToken.None);

        // Assert — dataRef deve ser 2026-05-19 (data BRT), não 2026-05-20 (data UTC)
        LocalDate dataEsperada = InstandeViraNoiteBrt.InZone(FusoBrasilia).Date; // 2026-05-19
        dataEsperada.Should().Be(new LocalDate(2026, 5, 19),
            because: "à 23:30 BRT do dia 19, ainda é dia 19 no Brasil — não 20 como em UTC");

        await cotacaoRepo.Received(1).GetMaisRecenteAsync(
            Moeda.Usd,
            TipoCotacao.SpotIntraday,
            Arg.Is<LocalDate>(d => d == dataEsperada),
            Arg.Any<CancellationToken>());
    }
}

using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;
using Sgcf.Infrastructure.Cambio;

using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Prove-It: CotacaoResolverService.ResolveAsync deve usar fuso BRT ao calcular dataRef.
///
/// Cenário crítico: 2026-05-19 23:30 BRT == 2026-05-20 02:30 UTC.
/// Com InUtc(), dataRef seria 2026-05-20 — e PtaxD1 consultaria D-1 = 2026-05-19.
/// Com InZone(BRT), dataRef é 2026-05-19 — e PtaxD1 consultaria D-1 = 2026-05-18.
/// A data BR (2026-05-19) é a correta para negócios dentro do horário bancário.
/// </summary>
[Trait("Category", "Domain")]
public sealed class CotacaoResolverServiceTimezoneTests
{
    private static readonly DateTimeZone FusoBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // 2026-05-19 23:30 BRT == 2026-05-20 02:30 UTC
    private static readonly Instant InstandeViraNoiteBrt = Instant.FromUtc(2026, 5, 20, 2, 30);

    private static ParametroCotacao CriarParametroGlobal(TipoCotacao tipo)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 11, 0, 0));
        return ParametroCotacao.Criar(null, null, tipo, clock);
    }

    private static CotacaoFx CriarCotacaoFx(Moeda moeda, TipoCotacao tipo, decimal compra, decimal venda) =>
        CotacaoFx.Criar(moeda, tipo,
            new Money(compra, Moeda.Brl),
            new Money(venda, Moeda.Brl),
            "BCB_OLINDA",
            Instant.FromUtc(2026, 5, 19, 18, 0));

    [Fact]
    public async Task ResolveAsync_As2330Brt_UsaDataRefBR_2026_05_19()
    {
        // Arrange — relógio fixo em 23:30 BRT (= 02:30 UTC do dia seguinte)
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();

        IReadOnlyList<ParametroCotacao> parametros = new[] { CriarParametroGlobal(TipoCotacao.PtaxD0) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametros);
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
            .Returns((Money?)null);

        CotacaoFx cotacao = CriarCotacaoFx(Moeda.Usd, TipoCotacao.PtaxD0, 5.10m, 5.14m);
        cotacaoRepo.GetMaisRecenteAsync(
                Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(cotacao);

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        // Act
        await sut.ResolveAsync(Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        // Assert — dataRef deve ser 2026-05-19 (data BR), não 2026-05-20 (data UTC)
        LocalDate dataEsperadaBrt = InstandeViraNoiteBrt.InZone(FusoBrasilia).Date; // 2026-05-19
        await cotacaoRepo.Received(1).GetMaisRecenteAsync(
            Moeda.Usd,
            TipoCotacao.PtaxD0,
            Arg.Is<LocalDate>(d => d == dataEsperadaBrt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_PtaxD1_As2330Brt_ConsultaD1ComDataBrt()
    {
        // PtaxD1 com relógio em 23:30 BRT → dataRef = 2026-05-19 → D-1 = 2026-05-18
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();

        IReadOnlyList<ParametroCotacao> parametros = new[] { CriarParametroGlobal(TipoCotacao.PtaxD1) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametros);
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
            .Returns((Money?)null);

        cotacaoRepo.GetMaisRecenteAsync(
                Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null);

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        // Act
        await sut.ResolveAsync(Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        // dataRef BRT = 2026-05-19; D-1 = 2026-05-18
        LocalDate dataD1EsperadaBrt = InstandeViraNoiteBrt.InZone(FusoBrasilia).Date.PlusDays(-1);
        dataD1EsperadaBrt.Should().Be(new LocalDate(2026, 5, 18));

        await cotacaoRepo.Received(1).GetMaisRecenteAsync(
            Moeda.Usd,
            TipoCotacao.PtaxD0,
            Arg.Is<LocalDate>(d => d == dataD1EsperadaBrt),
            Arg.Any<CancellationToken>());
    }
}

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

[Trait("Category", "Domain")]
public sealed class CotacaoResolverServiceTests
{
    private static IClock CriarClock(LocalDate dataAtual)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(dataAtual.AtMidnight().InUtc().ToInstant());
        return clock;
    }

    private static ParametroCotacao CriarParametroGlobal(TipoCotacao tipo)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 11, 0, 0));
        return ParametroCotacao.Criar(null, null, tipo, clock);
    }

    private static CotacaoFx CriarCotacaoFx(Moeda moeda, TipoCotacao tipo, decimal compra, decimal venda)
    {
        return CotacaoFx.Criar(
            moeda,
            tipo,
            new Money(compra, Moeda.Brl),
            new Money(venda, Moeda.Brl),
            "BCB_OLINDA",
            Instant.FromUtc(2026, 5, 11, 13, 15, 0));
    }

    // ── Teste 1: SpotIntraday + cache hit → retorna imediatamente, sem DB ─────

    [Fact]
    public async Task ResolveAsync_SpotIntradayCacheHit_RetornaImediato()
    {
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IClock clock = CriarClock(new LocalDate(2026, 5, 11));

        IReadOnlyList<ParametroCotacao> parametros = new[] { CriarParametroGlobal(TipoCotacao.SpotIntraday) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametros);

        Money cotacaoCache = new Money(5.30m, Moeda.Brl);
        spotCache.GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>()).Returns(cotacaoCache);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        ResultadoCotacao? resultado = await sut.ResolveAsync(
            Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.ValorMidRate.Valor.Should().Be(5.30m);
        resultado.Tipo.Should().Be(TipoCotacao.SpotIntraday);

        // DB must NOT have been queried
        await cotacaoRepo.DidNotReceive().GetMaisRecenteAsync(
            Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>());
    }

    // ── Teste 2: SpotIntraday + cache miss → consulta DB ─────────────────────

    [Fact]
    public async Task ResolveAsync_SpotIntradayCacheMiss_ConsultaDb()
    {
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IClock clock = CriarClock(new LocalDate(2026, 5, 11));

        IReadOnlyList<ParametroCotacao> parametros = new[] { CriarParametroGlobal(TipoCotacao.SpotIntraday) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametros);
        spotCache.GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>()).Returns((Money?)null);

        CotacaoFx cotacao = CriarCotacaoFx(Moeda.Usd, TipoCotacao.SpotIntraday, 5.28m, 5.32m);
        cotacaoRepo.GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.SpotIntraday, Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(cotacao);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        ResultadoCotacao? resultado = await sut.ResolveAsync(
            Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        resultado.Should().NotBeNull();
        // mid-rate = (5.28 + 5.32) / 2 = 5.30 rounded to 6dp
        resultado!.ValorMidRate.Valor.Should().Be(5.30m);
        resultado.Tipo.Should().Be(TipoCotacao.SpotIntraday);
    }

    // ── Teste 3: PtaxD0 → consulta DB com data de hoje ───────────────────────

    [Fact]
    public async Task ResolveAsync_PtaxD0_ConsultaDbComDataAtual()
    {
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        LocalDate dataAtual = new LocalDate(2026, 5, 11);
        IClock clock = CriarClock(dataAtual);

        IReadOnlyList<ParametroCotacao> parametrosPtaxD0 = new[] { CriarParametroGlobal(TipoCotacao.PtaxD0) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametrosPtaxD0);

        CotacaoFx cotacao = CriarCotacaoFx(Moeda.Usd, TipoCotacao.PtaxD0, 5.05m, 5.10m);
        cotacaoRepo.GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.PtaxD0, dataAtual, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        ResultadoCotacao? resultado = await sut.ResolveAsync(
            Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        resultado.Should().NotBeNull();
        // mid-rate = (5.05 + 5.10) / 2 = 5.075000
        resultado!.ValorMidRate.Valor.Should().Be(5.075m);
        resultado.Tipo.Should().Be(TipoCotacao.PtaxD0);

        await cotacaoRepo.Received(1).GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.PtaxD0, dataAtual, Arg.Any<CancellationToken>());
    }

    // ── Teste 4: PtaxD1 → consulta DB com D-1 usando tipo PtaxD0 ─────────────

    [Fact]
    public async Task ResolveAsync_PtaxD1_ConsultaDbComD1EUsaTipoPtaxD0()
    {
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        LocalDate dataAtual = new LocalDate(2026, 5, 11);
        LocalDate dataD1 = dataAtual.PlusDays(-1); // 2026-05-10
        IClock clock = CriarClock(dataAtual);

        IReadOnlyList<ParametroCotacao> parametrosPtaxD1 = new[] { CriarParametroGlobal(TipoCotacao.PtaxD1) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametrosPtaxD1);

        CotacaoFx cotacao = CriarCotacaoFx(Moeda.Usd, TipoCotacao.PtaxD0, 5.00m, 5.00m);
        cotacaoRepo.GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.PtaxD0, dataD1, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        ResultadoCotacao? resultado = await sut.ResolveAsync(
            Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.ValorMidRate.Valor.Should().Be(5.00m);
        // Tipo returned is PtaxD1 (what was requested), not PtaxD0 (what was queried)
        resultado.Tipo.Should().Be(TipoCotacao.PtaxD1);

        // Verify D-1 was used with PtaxD0 tipo
        await cotacaoRepo.Received(1).GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.PtaxD0, dataD1, Arg.Any<CancellationToken>());
    }

    // ── Teste 5: DB retorna null → retorna null ───────────────────────────────

    [Fact]
    public async Task ResolveAsync_SemCotacaoNoDb_RetornaNull()
    {
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IClock clock = CriarClock(new LocalDate(2026, 5, 11));

        IReadOnlyList<ParametroCotacao> parametrosNull = new[] { CriarParametroGlobal(TipoCotacao.PtaxD0) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametrosNull);
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
            .Returns((Money?)null);
        cotacaoRepo.GetMaisRecenteAsync(
            Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        ResultadoCotacao? resultado = await sut.ResolveAsync(
            Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        resultado.Should().BeNull();
    }

    // ── Teste 6: MidRate calculado corretamente ───────────────────────────────

    [Fact]
    public async Task ResolveAsync_CalculaMidRateCorretamente()
    {
        IParametroCotacaoRepository parametroRepo = Substitute.For<IParametroCotacaoRepository>();
        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IClock clock = CriarClock(new LocalDate(2026, 5, 11));

        IReadOnlyList<ParametroCotacao> parametrosMid = new[] { CriarParametroGlobal(TipoCotacao.PtaxD0) };
        parametroRepo.ListAtivosAsync(Arg.Any<CancellationToken>()).Returns(parametrosMid);

        // compra=5.8710, venda=5.8730 → mid = 5.8720
        CotacaoFx cotacao = CriarCotacaoFx(Moeda.Usd, TipoCotacao.PtaxD0, 5.8710m, 5.8730m);
        cotacaoRepo.GetMaisRecenteAsync(
            Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(cotacao);

        CotacaoResolverService sut = new(parametroRepo, cotacaoRepo, spotCache, clock);

        ResultadoCotacao? resultado = await sut.ResolveAsync(
            Moeda.Usd, Guid.NewGuid(), ModalidadeContrato.Finimp, CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.ValorMidRate.Valor.Should().Be(5.872m);
        resultado.ValorMidRate.Moeda.Should().Be(Moeda.Brl);
    }
}

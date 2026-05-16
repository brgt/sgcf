using System.Text.Json;

using FluentAssertions;

using MediatR;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Cambio;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Mcp.Tools;

using Xunit;

namespace Sgcf.Mcp.Tests.Tools;

[Trait("Category", "Mcp")]
public sealed class DividaToolsTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static PainelDividaDto CriarPainelDto() =>
        new(
            DataHoraCalculo: "2026-05-12T08:00:00Z",
            TipoCotacao: "SPOT_INTRADAY",
            BreakdownPorMoeda: new List<LinhaBreakdownMoedaDto>
            {
                new("Brl", 1_000_000m, 1m, 1_000_000m, 3)
            }.AsReadOnly(),
            DividaBrutaBrl: 1_000_000m,
            AjusteMtm: new AjusteMtmDto(0m, 0m, 0m),
            DividaLiquidaPosHedgeBrl: 1_000_000m,
            Alertas: new List<string>().AsReadOnly());

    private static DividaTools CriarTools(
        IMediator mediator,
        ICotacaoSpotCache? spotCache = null,
        ICotacaoFxRepository? cotacaoRepo = null,
        IClock? clock = null)
    {
        spotCache ??= Substitute.For<ICotacaoSpotCache>();
        cotacaoRepo ??= Substitute.For<ICotacaoFxRepository>();
        clock ??= CriarClock(Instant.FromUtc(2026, 5, 12, 8, 0));
        return new DividaTools(mediator, spotCache, cotacaoRepo, clock);
    }

    // ── GetPosicaoDivida ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPosicaoDivida_SemFiltros_EnviaPainelQuerySemFiltros()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetPainelDividaQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarPainelDto());
        DividaTools tools = CriarTools(mediator);

        // Act
        await tools.GetPosicaoDividaAsync(null, null, CancellationToken.None);

        // Assert — query deve ter BancoId == null e Modalidade == null
        await mediator.Received(1).Send(
            Arg.Is<GetPainelDividaQuery>(q => q.BancoId == null && q.Modalidade == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPosicaoDivida_BancoIdValido_EnviaQueryComBancoGuid()
    {
        // Arrange
        Guid bancoGuid = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetPainelDividaQuery>(), Arg.Any<CancellationToken>())
            .Returns(CriarPainelDto());
        DividaTools tools = CriarTools(mediator);

        // Act
        await tools.GetPosicaoDividaAsync(bancoGuid.ToString(), null, CancellationToken.None);

        // Assert
        await mediator.Received(1).Send(
            Arg.Is<GetPainelDividaQuery>(q => q.BancoId == bancoGuid),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPosicaoDivida_BancoIdInvalido_RetornaJsonDeErro()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        DividaTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetPosicaoDividaAsync("not-a-guid", null, CancellationToken.None);

        // Assert — deve retornar JSON com chave "error"
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out JsonElement errorElem).Should().BeTrue();
        errorElem.GetString().Should().Contain("bancoId");
        await mediator.DidNotReceive().Send(Arg.Any<GetPainelDividaQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPosicaoDivida_RetornaJsonSerializadoDoResult()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        PainelDividaDto dto = CriarPainelDto();
        mediator.Send(Arg.Any<GetPainelDividaQuery>(), Arg.Any<CancellationToken>())
            .Returns(dto);
        DividaTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetPosicaoDividaAsync(null, null, CancellationToken.None);

        // Assert — o JSON deve conter os campos do DTO
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("dividaBrutaBrl").GetDecimal().Should().Be(1_000_000m);
    }

    // ── GetCotacaoFx ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetCotacaoFx_MoedaInvalida_RetornaJsonDeErro()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        DividaTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetCotacaoFxAsync("XYZ", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetCotacaoFx_Brl_RetornaJsonDeErro()
    {
        // Arrange — BRL não é uma moeda estrangeira válida para cotação
        IMediator mediator = Substitute.For<IMediator>();
        DividaTools tools = CriarTools(mediator);

        // Act
        string resultado = await tools.GetCotacaoFxAsync("BRL", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetCotacaoFx_SpotDisponivel_RetornaTipoSpotIntraday()
    {
        // Arrange — spot cache retorna cotação
        IMediator mediator = Substitute.For<IMediator>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>())
            .Returns(new Money(5.10m, Moeda.Brl));
        DividaTools tools = CriarTools(mediator, spotCache: spotCache);

        // Act
        string resultado = await tools.GetCotacaoFxAsync("USD", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("tipo").GetString().Should().Be("SPOT_INTRADAY");
        doc.RootElement.GetProperty("valor_brl").GetDecimal().Should().Be(5.10m);
    }

    [Fact]
    public async Task GetCotacaoFx_SpotIndisponivelComPtax_RetornaTipoPtaxFallback()
    {
        // Arrange — spot cache retorna null; repo PTAX retorna cotação
        IMediator mediator = Substitute.For<IMediator>();

        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>())
            .Returns((Money?)null);

        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        Instant momento = Instant.FromUtc(2026, 5, 11, 18, 0);
        CotacaoFx ptax = CotacaoFx.Criar(
            moedaBase: Moeda.Usd,
            tipo: TipoCotacao.PtaxD1,
            valorCompra: new Money(5.08m, Moeda.Brl),
            valorVenda: new Money(5.12m, Moeda.Brl),
            fonte: "BCB",
            momento: momento);
        cotacaoRepo.GetMaisRecenteAsync(
            Moeda.Usd, TipoCotacao.PtaxD1,
            Arg.Any<LocalDate>(),
            Arg.Any<CancellationToken>())
            .Returns(ptax);

        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 12, 8, 0));
        DividaTools tools = CriarTools(mediator, spotCache, cotacaoRepo, clock);

        // Act
        string resultado = await tools.GetCotacaoFxAsync("USD", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.GetProperty("tipo").GetString().Should().Be("PTAX_D1_FALLBACK");
        // mid = (5.08 + 5.12) / 2 = 5.10
        doc.RootElement.GetProperty("valor_brl").GetDecimal().Should().Be(5.10m);
    }

    [Fact]
    public async Task GetCotacaoFx_TudoIndisponivel_RetornaJsonDeErro()
    {
        // Arrange — spot null, ptax null
        IMediator mediator = Substitute.For<IMediator>();

        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
            .Returns((Money?)null);

        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoRepo.GetMaisRecenteAsync(
            Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
            Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null);

        DividaTools tools = CriarTools(mediator, spotCache, cotacaoRepo);

        // Act
        string resultado = await tools.GetCotacaoFxAsync("EUR", CancellationToken.None);

        // Assert
        using JsonDocument doc = JsonDocument.Parse(resultado);
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }
}

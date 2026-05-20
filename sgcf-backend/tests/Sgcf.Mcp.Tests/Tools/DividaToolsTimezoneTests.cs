using System.Text.Json;

using FluentAssertions;

using MediatR;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Mcp.Tools;

using Xunit;

namespace Sgcf.Mcp.Tests.Tools;

/// <summary>
/// Prove-It: DividaTools.GetCotacaoFxAsync deve usar fuso BRT ao resolver LocalDate "hoje".
///
/// Cenário: 2026-05-19 23:30 BRT == 2026-05-20 02:30 UTC.
/// Com InUtc(), dataRef seria 2026-05-20 (errada para calendário BR).
/// Com InZone(BRT), dataRef é 2026-05-19 (correta para calendário BR).
/// </summary>
[Trait("Category", "Mcp")]
public sealed class DividaToolsTimezoneTests
{
    private static readonly DateTimeZone FusoBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // 2026-05-19 23:30 BRT == 2026-05-20 02:30 UTC
    private static readonly Instant InstandeViraNoiteBrt = Instant.FromUtc(2026, 5, 20, 2, 30);

    [Fact]
    public async Task GetCotacaoFx_As2330Brt_UsaDataLocalBR_Nao20()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();

        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>())
            .Returns((Money?)null); // força fallback PTAX para exercitar a query com dataRef

        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoRepo.GetMaisRecenteAsync(
                Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null); // resultado não importa — verificamos a data passada

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        DividaTools sut = new(mediator, spotCache, cotacaoRepo, clock);

        // Act
        await sut.GetCotacaoFxAsync("USD", CancellationToken.None);

        // Assert — a dataRef passada ao repositório deve ser 2026-05-19 (BRT), não 2026-05-20 (UTC)
        LocalDate dataEsperada = InstandeViraNoiteBrt.InZone(FusoBrasilia).Date; // 2026-05-19
        await cotacaoRepo.Received(1).GetMaisRecenteAsync(
            Moeda.Usd,
            TipoCotacao.PtaxD1,
            Arg.Is<LocalDate>(d => d == dataEsperada),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCotacaoFx_HorarioNormal_DataBrtEUtcCoincide_RetornaDataCorreta()
    {
        // Arrange — 2026-05-19 10:00 BRT == 2026-05-19 13:00 UTC (mesma data em ambos fusos)
        Instant instante = Instant.FromUtc(2026, 5, 19, 13, 0);
        IMediator mediator = Substitute.For<IMediator>();

        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>())
            .Returns((Money?)null);

        ICotacaoFxRepository cotacaoRepo = Substitute.For<ICotacaoFxRepository>();
        cotacaoRepo.GetMaisRecenteAsync(
                Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null);

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instante);

        DividaTools sut = new(mediator, spotCache, cotacaoRepo, clock);

        // Act
        await sut.GetCotacaoFxAsync("USD", CancellationToken.None);

        // Assert — 10:00 BRT = 2026-05-19 (igual em UTC também)
        LocalDate dataEsperada = new LocalDate(2026, 5, 19);
        await cotacaoRepo.Received(1).GetMaisRecenteAsync(
            Moeda.Usd,
            TipoCotacao.PtaxD1,
            Arg.Is<LocalDate>(d => d == dataEsperada),
            Arg.Any<CancellationToken>());
    }
}

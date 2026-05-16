using FluentAssertions;
using MediatR;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

[Trait("Category", "Unit")]
public sealed class CriarCotacaoCommandHandlerTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 5, 16, 9, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 16);
    private static readonly LocalDate DataPtax = new(2026, 5, 15);
    private const decimal PtaxUsdBrl = 5.20m;

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static CotacaoFx CriarCotacaoFx()
    {
        return CotacaoFx.Criar(
            Moeda.Usd,
            TipoCotacao.PtaxD1,
            new Money(PtaxUsdBrl - 0.05m, Moeda.Brl),
            new Money(PtaxUsdBrl, Moeda.Brl),
            "BACEN",
            // Momento deve ser no dia anterior à DataAbertura quando convertido para SP (UTC-3).
            // Agora = 2026-05-16T09:00Z; menos 10h = 2026-05-15T23:00Z = 2026-05-15T20:00-03:00.
            Agora.Minus(Duration.FromHours(10)));
    }

    [Fact]
    public async Task Handle_ComDadosValidos_CriaCotacaoComCodigoGerado()
    {
        // Arrange
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarCotacaoFx());
        repo.GerarProximoCodigoInternoAsync(DataAbertura.Year, default)
            .Returns("COT-2026-00001");

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock);
        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Finimp",
            ValorAlvoBrl: 500_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 5, 16));

        // Act
        CotacaoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.CodigoInterno.Should().Be("COT-2026-00001");
        resultado.Status.Should().Be(StatusCotacao.Rascunho.ToString());
        resultado.ValorAlvoBrl.Should().Be(500_000m);
        resultado.PtaxUsadaUsdBrl.Should().Be(PtaxUsdBrl);
        repo.Received(1).Add(Arg.Any<Cotacao>());
        await repo.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ComCodigoInternoInformado_UsaCodigoInformado()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarCotacaoFx());

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock);
        CriarCotacaoCommand cmd = new(
            CodigoInterno: "COT-MANUAL-001",
            Modalidade: "Finimp",
            ValorAlvoBrl: 1_000_000m,
            PrazoMaximoDias: 360,
            DataAbertura: new DateOnly(2026, 5, 16));

        CotacaoDto resultado = await handler.Handle(cmd, default);

        resultado.CodigoInterno.Should().Be("COT-MANUAL-001");
        // Não deve ter gerado código pelo repositório
        await repo.DidNotReceive().GerarProximoCodigoInternoAsync(Arg.Any<int>(), default);
    }

    [Fact]
    public async Task Handle_QuandoPtaxIndisponivel_LancaInvalidOperationException()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();

        // PTAX não disponível
        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns((CotacaoFx?)null);

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock);
        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Finimp",
            ValorAlvoBrl: 500_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 5, 16));

        // Act & Assert
        Func<Task> act = () => handler.Handle(cmd, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PTAX D-1*");
    }
}

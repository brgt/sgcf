using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Hedge;
using Sgcf.Application.Hedge.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Hedge;
using Xunit;

namespace Sgcf.Application.Tests.Hedge;

/// <summary>
/// Testes unitários para <see cref="RegistrarHistoricoMtmCommandHandler"/>.
/// Todos os repositórios são substituídos por NSubstitute — sem Testcontainers.
/// </summary>
public sealed class RegistrarHistoricoMtmCommandHandlerTests
{
    private static readonly Instant InstanteFixo  = Instant.FromUtc(2026, 5, 21, 12, 0, 0);
    private static readonly LocalDate DataFixa     = new(2026, 5, 21);
    private static readonly Guid HedgeIdFixo       = Guid.NewGuid();

    // ── Fábrica de dependências ───────────────────────────────────────────────

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    private static IHedgeRepository CriarHedgeRepoComHedge()
    {
        IHedgeRepository repo = Substitute.For<IHedgeRepository>();

        // Cria um hedge mínimo para o teste — precisamos apenas que GetByIdAsync não retorne null.
        InstrumentoHedge hedge = InstrumentoHedge.CriarForward(
            contratoId:    Guid.NewGuid(),
            contraparteId: Guid.NewGuid(),
            notional:      new Money(100_000m, Moeda.Usd),
            dataCont:      new LocalDate(2026, 1, 1),
            dataVenc:      new LocalDate(2026, 12, 31),
            strikeForward: 5.0m,
            clock:         CriarClock());

        repo.GetByIdAsync(HedgeIdFixo, Arg.Any<CancellationToken>()).Returns(hedge);

        return repo;
    }

    // ── Caminho de criação ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_QuandoNaoExisteRegistro_ChamaAddESalva()
    {
        // Arrange
        IHedgeRepository hedgeRepo     = CriarHedgeRepoComHedge();
        IHistoricoMtmRepository repo   = Substitute.For<IHistoricoMtmRepository>();
        RegistrarHistoricoMtmCommandHandler handler = new(hedgeRepo, repo, CriarClock());

        // Repositório retorna null → caminho de criação.
        repo.GetAsync(HedgeIdFixo, DataFixa, Arg.Any<CancellationToken>())
            .Returns((HistoricoMtmDiario?)null);

        var command = new RegistrarHistoricoMtmCommand(
            HedgeId:        HedgeIdFixo,
            DataReferencia: "2026-05-21",
            PayoffBrl:      1_500m,
            SpotUtilizado:  5.25m,
            TipoCotacao:    "SPOT_INTRADAY");

        // Act
        HistoricoMtmDiarioDto result = await handler.Handle(command, CancellationToken.None);

        // Assert — Add deve ser chamado exatamente uma vez, SaveChanges também.
        repo.Received(1).Add(Arg.Any<HistoricoMtmDiario>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        result.PayoffBrl.Should().Be(1_500m);
        result.Posicao.Should().Be("RECEBER");
        result.TipoCotacao.Should().Be("SPOT_INTRADAY");
        result.DataReferencia.Should().Be("2026-05-21");
    }

    // ── Caminho de atualização ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_QuandoRegistroJaExiste_NaoChamaAddMasSalva()
    {
        // Arrange
        IHedgeRepository hedgeRepo   = CriarHedgeRepoComHedge();
        IHistoricoMtmRepository repo = Substitute.For<IHistoricoMtmRepository>();
        RegistrarHistoricoMtmCommandHandler handler = new(hedgeRepo, repo, CriarClock());

        // Cria o snapshot existente que o repositório deve devolver.
        HistoricoMtmDiario existente = HistoricoMtmDiario.Criar(
            HedgeIdFixo, DataFixa, 500m, 5.10m, "SPOT_INTRADAY", InstanteFixo);

        repo.GetAsync(HedgeIdFixo, DataFixa, Arg.Any<CancellationToken>())
            .Returns(existente);

        var command = new RegistrarHistoricoMtmCommand(
            HedgeId:        HedgeIdFixo,
            DataReferencia: "2026-05-21",
            PayoffBrl:      -300m,
            SpotUtilizado:  5.30m,
            TipoCotacao:    "PTAX_D1");

        // Act
        HistoricoMtmDiarioDto result = await handler.Handle(command, CancellationToken.None);

        // Assert — Add NÃO deve ser chamado; SaveChanges sim.
        repo.DidNotReceive().Add(Arg.Any<HistoricoMtmDiario>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        result.PayoffBrl.Should().Be(-300m);
        result.Posicao.Should().Be("PAGAR");
        result.TipoCotacao.Should().Be("PTAX_D1");
    }

    // ── Hedge não encontrado ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_QuandoHedgeNaoExiste_LancaKeyNotFoundException()
    {
        IHedgeRepository hedgeRepo   = Substitute.For<IHedgeRepository>();
        IHistoricoMtmRepository repo = Substitute.For<IHistoricoMtmRepository>();

        hedgeRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns((InstrumentoHedge?)null);

        RegistrarHistoricoMtmCommandHandler handler = new(hedgeRepo, repo, CriarClock());

        var command = new RegistrarHistoricoMtmCommand(
            HedgeId:        Guid.NewGuid(),
            DataReferencia: "2026-05-21",
            PayoffBrl:      100m,
            SpotUtilizado:  5.0m);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}

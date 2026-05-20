using FluentAssertions;

using MediatR;

using NodaTime;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Sgcf.Application.Painel.Queries;
using Sgcf.Application.Simulacao;
using Sgcf.Application.Simulacao.Queries;
using Sgcf.Application.Tests.Simulacao.Helpers;
using Sgcf.Domain.Common;
using Sgcf.Domain.Simulacao;

using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Queries;

/// <summary>
/// Testes unitários para <see cref="CompararCenariosQueryHandler"/>.
///
/// Estratégia: mock de <see cref="IMediator"/> para interceptar chamadas a
/// <see cref="GetQuadroDividaQuery"/> sem precisar de banco real.
/// Os DTOs retornados pelo mock representam projeções simplificadas com valores
/// conhecidos, permitindo assertions numéricas precisas sobre os deltas.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CompararCenariosQueryHandlerTests
{
    private readonly IClock _clock = CenarioSimulacaoTestFactory.CriarClock();

    // ── Helpers de DTO ────────────────────────────────────────────────────────

    /// <summary>
    /// Constrói um <see cref="QuadroDividaDto"/> mínimo com 12 meses cujos
    /// <c>SaldoTotalFim</c>, <c>TotalAmortizacaoMes</c> e <c>TotalCaptacaoMes</c>
    /// são constantes ao longo do ano — suficiente para os testes de delta.
    /// </summary>
    private static QuadroDividaDto CriarQuadroSimples(
        int ano,
        decimal saldoFimMensal,
        decimal totalCaptacaoMensal,
        decimal totalAmortizacaoMensal,
        Guid? cenarioId = null)
    {
        List<MesProjecaoDto> meses = Enumerable.Range(1, SimulacaoTestConstants.MesesNoAno)
            .Select(mes => new MesProjecaoDto(
                Ano: ano,
                Mes: mes,
                Bancos: Array.Empty<SaldoBancoMesDto>().AsReadOnly(),
                SaldoTotalInicio: saldoFimMensal,
                SaldoTotalFim: saldoFimMensal,
                TotalAmortizacaoMes: totalAmortizacaoMensal,
                TotalCaptacaoMes: totalCaptacaoMensal))
            .ToList();

        QuadroDividaSumarioDto sumario = new(
            SaldoTotalInicioAno: saldoFimMensal,
            SaldoTotalFimAno: saldoFimMensal,
            TotalAmortizacaoNoAno: totalAmortizacaoMensal * SimulacaoTestConstants.MesesNoAno,
            TotalCaptacaoNoAno: totalCaptacaoMensal * SimulacaoTestConstants.MesesNoAno,
            VariacaoAnualPercentual: 0m);

        SaldoPorBancoAtualDto snapshot = new(
            Bancos: Array.Empty<SaldoBancoAtualDto>().AsReadOnly(),
            SaldoTotalBrl: saldoFimMensal,
            DataReferencia: new LocalDate(ano, 1, 1));

        CenarioAplicadoDto? cenarioAplicado = cenarioId.HasValue
            ? new CenarioAplicadoDto(cenarioId.Value, "Cenário Teste", "Ativo", ano, 0)
            : null;

        return new QuadroDividaDto(
            Ano: ano,
            DataReferencia: new DateOnly(ano, 1, 1),
            SnapshotInicial: snapshot,
            Projecao: new QuadroDividaProjecaoDto(meses.AsReadOnly()),
            Sumario: sumario,
            Alertas: Array.Empty<string>().AsReadOnly(),
            CenarioAplicado: cenarioAplicado);
    }

    // ── Teste 1: baseline com deltas zero + outro cenário com deltas reais ────

    /// <summary>
    /// Quando dois cenários são comparados, o primeiro (baseline) deve ter
    /// <c>DeltasMensais == null</c> e <c>DeltaAnual == null</c>.
    /// O segundo deve ter deltas calculados em relação ao baseline.
    /// </summary>
    [Fact]
    public async Task Handle_doisCenarios_retornaBaselineComDeltasNulos_eOutroComDeltasReais()
    {
        // Arrange
        int ano = SimulacaoTestConstants.AnoBaseDefault;
        Guid cenarioIdBaseline = Guid.NewGuid();
        Guid cenarioIdB = Guid.NewGuid();

        IMediator mediator = Substitute.For<IMediator>();

        QuadroDividaDto quadroBaseline = CriarQuadroSimples(ano, saldoFimMensal: 1_000_000m, totalCaptacaoMensal: 100_000m, totalAmortizacaoMensal: 50_000m);
        QuadroDividaDto quadroB = CriarQuadroSimples(ano, saldoFimMensal: 1_200_000m, totalCaptacaoMensal: 150_000m, totalAmortizacaoMensal: 50_000m);

        mediator.Send(Arg.Is<GetQuadroDividaQuery>(q => q.CenarioId == cenarioIdBaseline), Arg.Any<CancellationToken>())
            .Returns(quadroBaseline);
        mediator.Send(Arg.Is<GetQuadroDividaQuery>(q => q.CenarioId == cenarioIdB), Arg.Any<CancellationToken>())
            .Returns(quadroB);

        // Cenários mock com mesmo AnoBase
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CenarioSimulacao cBaseline = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "Baseline", anoBase: ano);
        CenarioSimulacao cB = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "Cenário B", anoBase: ano);
        repo.GetByIdAsync(cenarioIdBaseline, Arg.Any<CancellationToken>()).Returns(cBaseline);
        repo.GetByIdAsync(cenarioIdB, Arg.Any<CancellationToken>()).Returns(cB);
        repo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CenarioSimulacao> { cBaseline, cB }.AsReadOnly());

        CompararCenariosQueryHandler handler = new(mediator, repo);
        CompararCenariosQuery query = new(ano, new List<Guid> { cenarioIdBaseline, cenarioIdB }.AsReadOnly());

        // Act
        ResultadoComparacaoCenariosDto resultado = await handler.Handle(query, default);

        // Assert — estrutura geral
        resultado.Ano.Should().Be(ano);
        resultado.Cenarios.Should().HaveCount(2);

        CenarioComparadoDto baseline = resultado.Cenarios[0];
        baseline.EhBaseline.Should().BeTrue("primeiro cenário é sempre baseline");
        baseline.DeltasMensais.Should().BeNull("baseline não tem deltas");
        baseline.DeltaAnual.Should().BeNull("baseline não tem delta anual");

        CenarioComparadoDto cenarioB = resultado.Cenarios[1];
        cenarioB.EhBaseline.Should().BeFalse();
        cenarioB.DeltasMensais.Should().HaveCount(12);
        cenarioB.DeltaAnual.Should().NotBeNull();

        // Delta mensal do mês 1: 1.200.000 - 1.000.000 = 200.000
        cenarioB.DeltasMensais![0].SaldoFimDelta.Should().Be(200_000m);
        cenarioB.DeltasMensais[0].TotalCaptacaoDelta.Should().Be(50_000m);
        cenarioB.DeltaAnual!.SaldoFimAnoDelta.Should().Be(200_000m);
    }

    // ── Teste 2: lista vazia de cenarioIds → ArgumentException ────────────────

    /// <summary>
    /// Lista vazia de <c>CenarioIds</c> deve lançar <see cref="ArgumentException"/>
    /// imediatamente — sem consultar o mediator.
    /// </summary>
    [Fact]
    public async Task Handle_cenarioIdsVazio_lancaArgumentException()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CompararCenariosQueryHandler handler = new(mediator, repo);

        CompararCenariosQuery query = new(SimulacaoTestConstants.AnoBaseDefault, new List<Guid>().AsReadOnly());

        // Act & Assert
        Func<Task> act = () => handler.Handle(query, default);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cenário*");

        await mediator.DidNotReceive().Send(Arg.Any<IRequest<QuadroDividaDto>>(), Arg.Any<CancellationToken>());
    }

    // ── Teste 3: mais de 5 cenários → ArgumentException ──────────────────────

    /// <summary>
    /// Mais de 5 cenários devem ser rejeitados com <see cref="ArgumentException"/>
    /// antes de qualquer chamada ao mediator (guarda rápida no topo do handler).
    /// </summary>
    [Fact]
    public async Task Handle_maisDe5Cenarios_lancaArgumentException()
    {
        // Arrange
        IMediator mediator = Substitute.For<IMediator>();
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        CompararCenariosQueryHandler handler = new(mediator, repo);

        List<Guid> seisIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        CompararCenariosQuery query = new(SimulacaoTestConstants.AnoBaseDefault, seisIds.AsReadOnly());

        // Act & Assert
        Func<Task> act = () => handler.Handle(query, default);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*5*");

        await mediator.DidNotReceive().Send(Arg.Any<IRequest<QuadroDividaDto>>(), Arg.Any<CancellationToken>());
    }

    // ── Teste 4: cenário inexistente → KeyNotFoundException propagada ─────────

    /// <summary>
    /// Quando o repositório não encontra um cenário, a <see cref="KeyNotFoundException"/>
    /// deve ser propagada — o controller a mapeia para 404.
    /// </summary>
    [Fact]
    public async Task Handle_cenarioInexistente_lancaKeyNotFoundException()
    {
        // Arrange
        Guid idInexistente = Guid.NewGuid();
        IMediator mediator = Substitute.For<IMediator>();
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();
        repo.GetByIdAsync(idInexistente, Arg.Any<CancellationToken>()).Returns((CenarioSimulacao?)null);
        repo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CenarioSimulacao>().AsReadOnly());

        CompararCenariosQueryHandler handler = new(mediator, repo);
        CompararCenariosQuery query = new(SimulacaoTestConstants.AnoBaseDefault, new List<Guid> { idInexistente }.AsReadOnly());

        // Act & Assert
        Func<Task> act = () => handler.Handle(query, default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Teste 5: cenários com AnoBase diferente → InvalidOperationException ───

    /// <summary>
    /// Dois cenários com <c>AnoBase</c> diferentes devem ser rejeitados com
    /// <see cref="InvalidOperationException"/> antes de qualquer chamada ao mediator.
    /// O controller mapeia para 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Handle_cenariosComAnoBaseDiferente_lancaInvalidOperationException()
    {
        // Arrange
        Guid idA = Guid.NewGuid();
        Guid idB = Guid.NewGuid();

        IMediator mediator = Substitute.For<IMediator>();
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();

        CenarioSimulacao cenarioA = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "A", anoBase: SimulacaoTestConstants.AnoBaseDefault);
        CenarioSimulacao cenarioB = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "B", anoBase: 2025);

        repo.GetByIdAsync(idA, Arg.Any<CancellationToken>()).Returns(cenarioA);
        repo.GetByIdAsync(idB, Arg.Any<CancellationToken>()).Returns(cenarioB);
        repo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CenarioSimulacao> { cenarioA, cenarioB }.AsReadOnly());

        CompararCenariosQueryHandler handler = new(mediator, repo);
        CompararCenariosQuery query = new(SimulacaoTestConstants.AnoBaseDefault, new List<Guid> { idA, idB }.AsReadOnly());

        // Act & Assert
        Func<Task> act = () => handler.Handle(query, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AnoBase*");

        await mediator.DidNotReceive().Send(Arg.Any<IRequest<QuadroDividaDto>>(), Arg.Any<CancellationToken>());
    }

    // ── Teste 6 (gap): baseline com custo zero → percentual retorna 0 sem DivideByZero ──

    /// <summary>
    /// Cobertura do gap: quando o baseline tem <c>SaldoTotalFim == 0</c> em todos os meses
    /// e <c>SaldoTotalFimAno == 0</c>, o cálculo de <c>SaldoFimDeltaPercentual</c> deve
    /// retornar 0 — nunca lançar <see cref="DivideByZeroException"/> nem <c>Infinity</c>.
    ///
    /// O handler já trata o caso via <c>baseline == 0m ? 0m : ...</c>.
    /// Este teste documenta e protege esse comportamento.
    /// </summary>
    [Fact]
    public async Task Handle_BaselineSaldoZero_NaoLancaDivideByZero_RetornaPercentualZero()
    {
        // Arrange — baseline sem saldo (empresa sem dívida), cenário B com captação
        int ano = SimulacaoTestConstants.AnoBaseDefault;
        Guid cenarioIdBaseline = Guid.NewGuid();
        Guid cenarioIdB = Guid.NewGuid();

        IMediator mediator = Substitute.For<IMediator>();
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();

        // Baseline: saldo zero em todos os meses, captação zero
        QuadroDividaDto quadroBaseline = CriarQuadroSimples(ano,
            saldoFimMensal: 0m,
            totalCaptacaoMensal: 0m,
            totalAmortizacaoMensal: 0m);

        // Cenário B: captação de R$ 500k
        QuadroDividaDto quadroB = CriarQuadroSimples(ano,
            saldoFimMensal: 500_000m,
            totalCaptacaoMensal: 500_000m,
            totalAmortizacaoMensal: 0m);

        mediator.Send(Arg.Is<GetQuadroDividaQuery>(q => q.CenarioId == cenarioIdBaseline), Arg.Any<CancellationToken>())
            .Returns(quadroBaseline);
        mediator.Send(Arg.Is<GetQuadroDividaQuery>(q => q.CenarioId == cenarioIdB), Arg.Any<CancellationToken>())
            .Returns(quadroB);

        CenarioSimulacao cBaseline = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "Baseline Vazio", anoBase: ano);
        CenarioSimulacao cB = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "Cenário B", anoBase: ano);
        repo.GetByIdAsync(cenarioIdBaseline, Arg.Any<CancellationToken>()).Returns(cBaseline);
        repo.GetByIdAsync(cenarioIdB, Arg.Any<CancellationToken>()).Returns(cB);
        repo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CenarioSimulacao> { cBaseline, cB }.AsReadOnly());

        CompararCenariosQueryHandler handler = new(mediator, repo);
        CompararCenariosQuery query = new(ano, new List<Guid> { cenarioIdBaseline, cenarioIdB }.AsReadOnly());

        // Act — não deve lançar DivideByZeroException nem overflow
        ResultadoComparacaoCenariosDto resultado = await handler.Handle(query, default);

        // Assert — percentuais devem ser 0 (baseline zero → divisão por zero protegida)
        DeltaMensalDto deltaMes1 = resultado.Cenarios[1].DeltasMensais![0];
        deltaMes1.SaldoFimDelta.Should().Be(500_000m,
            "delta absoluto = cenário - baseline = 500k - 0k = 500k");
        deltaMes1.SaldoFimDeltaPercentual.Should().Be(0m,
            "percentual deve ser 0 quando baseline é zero (evita DivideByZeroException)");

        DeltaAnualDto deltaAnual = resultado.Cenarios[1].DeltaAnual!;
        deltaAnual.SaldoFimAnoDelta.Should().Be(500_000m);
        deltaAnual.SaldoFimAnoDeltaPercentual.Should().Be(0m,
            "percentual anual deve ser 0 quando SaldoTotalFimAno do baseline é zero");
    }

    // ── Teste 7: cálculo correto de delta mensal e anual ─────────────────────

    /// <summary>
    /// Verifica que o cálculo de <c>SaldoFimDeltaPercentual</c> é feito corretamente
    /// como <c>(delta / baseline) × 100</c>, e que <c>DeltaAnual</c> usa os valores
    /// do <c>Sumario</c> (não dos meses individuais).
    /// </summary>
    [Fact]
    public async Task Handle_calcula_deltaMensal_e_deltaAnual_corretamente()
    {
        // Arrange
        int ano = SimulacaoTestConstants.AnoBaseDefault;
        Guid cenarioIdBaseline = Guid.NewGuid();
        Guid cenarioIdB = Guid.NewGuid();

        IMediator mediator = Substitute.For<IMediator>();
        ICenarioSimulacaoRepository repo = Substitute.For<ICenarioSimulacaoRepository>();

        // Baseline: SaldoFim = 1.000.000 por mês, SaldoFimAno = 1.000.000
        QuadroDividaDto quadroBaseline = CriarQuadroSimples(ano,
            saldoFimMensal: 1_000_000m,
            totalCaptacaoMensal: 0m,
            totalAmortizacaoMensal: 0m);

        // Cenário B: SaldoFim = 1.500.000 por mês, SaldoFimAno = 1.500.000
        QuadroDividaDto quadroB = CriarQuadroSimples(ano,
            saldoFimMensal: 1_500_000m,
            totalCaptacaoMensal: 500_000m,
            totalAmortizacaoMensal: 0m);

        mediator.Send(Arg.Is<GetQuadroDividaQuery>(q => q.CenarioId == cenarioIdBaseline), Arg.Any<CancellationToken>())
            .Returns(quadroBaseline);
        mediator.Send(Arg.Is<GetQuadroDividaQuery>(q => q.CenarioId == cenarioIdB), Arg.Any<CancellationToken>())
            .Returns(quadroB);

        CenarioSimulacao cBaseline = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "Baseline", anoBase: ano);
        CenarioSimulacao cB = CenarioSimulacaoTestFactory.CriarCenarioRascunho(_clock, "B", anoBase: ano);
        repo.GetByIdAsync(cenarioIdBaseline, Arg.Any<CancellationToken>()).Returns(cBaseline);
        repo.GetByIdAsync(cenarioIdB, Arg.Any<CancellationToken>()).Returns(cB);
        repo.GetByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CenarioSimulacao> { cBaseline, cB }.AsReadOnly());

        CompararCenariosQueryHandler handler = new(mediator, repo);
        CompararCenariosQuery query = new(ano, new List<Guid> { cenarioIdBaseline, cenarioIdB }.AsReadOnly());

        // Act
        ResultadoComparacaoCenariosDto resultado = await handler.Handle(query, default);

        // Assert — percentual = (500.000 / 1.000.000) × 100 = 50%
        DeltaMensalDto deltaMes1 = resultado.Cenarios[1].DeltasMensais![0];
        deltaMes1.SaldoFimDelta.Should().Be(500_000m);
        deltaMes1.TotalCaptacaoDelta.Should().Be(500_000m);
        deltaMes1.SaldoFimDeltaPercentual.Should().Be(50m,
            "percentual = (500.000 / 1.000.000) × 100 = 50%");

        // Delta anual: SaldoFimAno cenário B (1.500.000) - baseline (1.000.000) = 500.000
        DeltaAnualDto deltaAnual = resultado.Cenarios[1].DeltaAnual!;
        deltaAnual.SaldoFimAnoDelta.Should().Be(500_000m);
        deltaAnual.SaldoFimAnoDeltaPercentual.Should().Be(50m);
        deltaAnual.TotalCaptacaoAnoDelta.Should().Be(500_000m * SimulacaoTestConstants.MesesNoAno);
    }

}

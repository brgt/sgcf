using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

using Xunit;

namespace Sgcf.Domain.Tests.Simulacao;

/// <summary>
/// Testes unitários para <see cref="CenarioSimulacao.DuplicarComoRascunho"/>.
/// Cobre a factory D-10 (Task 2.1b da SPEC §6.2).
/// </summary>
[Trait("Category", "Domain")]
public sealed class CenarioSimulacaoDuplicarTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly Instant T0 = Instant.FromUtc(2026, 5, 19, 10, 0, 0);
    private static readonly Instant T1 = T0.Plus(Duration.FromDays(1));

    private static IClock ClockEm(Instant instant)
    {
        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static IClock ClockT0() => ClockEm(T0);
    private static IClock ClockT1() => ClockEm(T1);

    private static SimulacaoContratacao CriarSimulacaoValida(Guid cenarioId)
    {
        return SimulacaoContratacao.Criar(
            cenarioId: cenarioId,
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.CapitalDeGiro,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 7, 15),
            dataPrimeiroVencimento: new LocalDate(2026, 8, 15),
            tipoTaxa: TipoTaxa.CdiSpread,
            taxaAa: null,
            spreadAa: Percentual.De(3m),
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Bullet,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaFixo,
            anchorDiaFixo: 15,
            garantiaExigidaPrevista: "CDB 20%",
            observacoes: "obs original",
            clock: ClockT0());
    }

    private static CenarioSimulacao CriarCenarioRascunho(string nome = "Realista 2026", int anoBase = 2026)
    {
        CenarioSimulacao cenario = CenarioSimulacao.Criar(nome, anoBase, "original@test.com", ClockT0());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockT0());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockT0());
        return cenario;
    }

    // ── 27. DuplicarComoRascunho_simples_retornaNovoCenarioEmRascunho ─────────

    [Fact]
    public void DuplicarComoRascunho_simples_retornaNovoCenarioEmRascunho()
    {
        // Arrange
        CenarioSimulacao original = CriarCenarioRascunho();

        // Act
        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(original, "novo@test.com", ClockT1());

        // Assert
        copia.Status.Should().Be(StatusCenarioSimulacao.Rascunho,
            because: "D-10: cópia sempre começa como Rascunho");
    }

    // ── 28. DuplicarComoRascunho_sufixaNomeComCopia ───────────────────────────

    [Fact]
    public void DuplicarComoRascunho_sufixaNomeComCopia()
    {
        // Arrange
        CenarioSimulacao original = CriarCenarioRascunho(nome: "Realista 2026");

        // Act
        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(original, "novo@test.com", ClockT1());

        // Assert
        copia.Nome.Should().Be("Realista 2026 (cópia)",
            because: "D-10: nome sufixado exatamente com \" (cópia)\"");
    }

    // ── 29. DuplicarComoRascunho_copiaProfundaDeSimulacoes ────────────────────

    [Fact]
    public void DuplicarComoRascunho_copiaProfundaDeSimulacoes()
    {
        // Arrange
        CenarioSimulacao original = CriarCenarioRascunho();
        int qtdOriginal = original.Simulacoes.Count;

        // Act
        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(original, "novo@test.com", ClockT1());

        // Assert — mesma quantidade de simulações
        copia.Simulacoes.Should().HaveCount(qtdOriginal,
            because: "cópia profunda preserva todas as simulações filhas");

        // Todos os Ids das simulações da cópia devem ser distintos dos originais
        IEnumerable<Guid> idsOriginais = original.Simulacoes.Select(s => s.Id);
        IEnumerable<Guid> idsCopia = copia.Simulacoes.Select(s => s.Id);
        idsCopia.Should().NotIntersectWith(idsOriginais,
            because: "simulações copiadas recebem novos Ids");
    }

    // ── 30. DuplicarComoRascunho_geraNovoCenarioId ───────────────────────────

    [Fact]
    public void DuplicarComoRascunho_geraNovoCenarioId_naoIgualAoOriginal()
    {
        // Arrange
        CenarioSimulacao original = CriarCenarioRascunho();

        // Act
        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(original, "novo@test.com", ClockT1());

        // Assert
        copia.Id.Should().NotBe(original.Id,
            because: "cópia é agregado independente com novo Guid");
    }

    // ── 31. DuplicarComoRascunho_deCenarioArquivado_funciona ─────────────────

    [Fact]
    public void DuplicarComoRascunho_deCenarioArquivado_funciona()
    {
        // Arrange — cenário arquivado pode ser duplicado (SPEC §6.2)
        CenarioSimulacao original = CriarCenarioRascunho();
        original.Ativar(ClockT0());
        original.Arquivar(ClockT0());
        original.Status.Should().Be(StatusCenarioSimulacao.Arquivado);

        // Act — duplicar arquivado não deve lançar
        Action ato = () => CenarioSimulacao.DuplicarComoRascunho(original, "novo@test.com", ClockT1());

        // Assert
        ato.Should().NotThrow(because: "D-10: duplicar cenário arquivado é operação permitida");
    }

    // ── 32. DuplicarComoRascunho_preserveAnoBase ─────────────────────────────

    [Fact]
    public void DuplicarComoRascunho_preserveAnoBase()
    {
        // Arrange
        CenarioSimulacao original = CriarCenarioRascunho(anoBase: 2027);

        // Act
        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(original, "novo@test.com", ClockEm(
            Instant.FromUtc(2026, 5, 19, 10, 0, 0)));

        // Assert
        copia.AnoBase.Should().Be(2027,
            because: "AnoBase do original é preservado na cópia");
    }

    // ── 33. DuplicarComoRascunho_atribuiCriadoPorEUpdatedAt ──────────────────

    [Fact]
    public void DuplicarComoRascunho_atribuiCriadoPorEUpdatedAt_doNovoCenario()
    {
        // Arrange
        CenarioSimulacao original = CriarCenarioRascunho();
        string novoCriadoPor = "duplicador@test.com";

        // Act
        CenarioSimulacao copia = CenarioSimulacao.DuplicarComoRascunho(original, novoCriadoPor, ClockT1());

        // Assert
        copia.CriadoPor.Should().Be(novoCriadoPor,
            because: "D-10: CriadoPor = caller da duplicação, não o original");
        copia.CreatedAt.Should().Be(T1,
            because: "D-10: CreatedAt = instante da duplicação");
        copia.UpdatedAt.Should().Be(T1,
            because: "D-10: UpdatedAt = instante da duplicação");

        // CriadoPor do original permanece inalterado
        original.CriadoPor.Should().Be("original@test.com",
            because: "original não é afetado pela duplicação");
    }
}

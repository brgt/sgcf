using FluentAssertions;

using FsCheck;
using FsCheck.Xunit;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

using Xunit;

namespace Sgcf.Domain.Tests.Simulacao;

/// <summary>
/// Testes unitários para <see cref="CenarioSimulacao"/>.
/// Cobre: criação, lifecycle (Rascunho → Ativo → Arquivado) e gestão de simulações.
/// Spec §6.2 (ciclo de vida) + §6.3 (invariantes).
/// </summary>
[Trait("Category", "Domain")]
public sealed class CenarioSimulacaoTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly Instant T0 = Instant.FromUtc(2026, 5, 19, 10, 0, 0);

    private static IClock ClockEm(Instant instant)
    {
        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static IClock ClockPadrao() => ClockEm(T0);

    private static SimulacaoContratacao CriarSimulacaoValida(Guid cenarioId, int anoBase = 2026)
    {
        var clock = ClockPadrao();
        // DataContratacaoPrevista em 2026-07-15 (dentro do anoBase e >= hoje em T0)
        return SimulacaoContratacao.Criar(
            cenarioId: cenarioId,
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.CapitalDeGiro,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(anoBase, 7, 15),
            dataPrimeiroVencimento: new LocalDate(anoBase, 8, 15),
            tipoTaxa: TipoTaxa.CdiSpread,
            taxaAa: null,
            spreadAa: Percentual.De(3m),
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Bullet,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaFixo,
            anchorDiaFixo: 15,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: clock);
    }

    // ── 1. Criar_comNomeValido_temStatusRascunho ──────────────────────────────

    [Fact]
    public void Criar_comNomeValido_temStatusRascunho()
    {
        // Arrange / Act
        CenarioSimulacao cenario = CenarioSimulacao.Criar(
            nome: "Realista 2026",
            anoBase: 2026,
            criadoPor: "user@example.com",
            clock: ClockPadrao());

        // Assert
        cenario.Status.Should().Be(StatusCenarioSimulacao.Rascunho);
        cenario.Nome.Should().Be("Realista 2026");
        cenario.AnoBase.Should().Be(2026);
        cenario.CriadoPor.Should().Be("user@example.com");
    }

    // ── 2. Criar_comNomeVazio_lancaExcecao ───────────────────────────────────

    [Fact]
    public void Criar_comNomeVazio_lancaExcecao()
    {
        // Act
        Action ato = () => CenarioSimulacao.Criar(
            nome: "",
            anoBase: 2026,
            criadoPor: "user@example.com",
            clock: ClockPadrao());

        // Assert
        ato.Should().Throw<ArgumentException>();
    }

    // ── 3. Criar_comAnoBaseInvalido_lancaExcecao ─────────────────────────────

    [Theory]
    [InlineData(2019)]
    [InlineData(2051)]
    public void Criar_comAnoBaseInvalido_lancaExcecao(int anoInvalido)
    {
        // Act
        Action ato = () => CenarioSimulacao.Criar(
            nome: "Cenario X",
            anoBase: anoInvalido,
            criadoPor: "user@example.com",
            clock: ClockPadrao());

        // Assert
        ato.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── 4. Criar_capturaCriadoPorEUpdatedAt ──────────────────────────────────

    [Fact]
    public void Criar_capturaCriadoPorEUpdatedAt()
    {
        // Arrange
        var clock = ClockEm(T0);

        // Act
        CenarioSimulacao cenario = CenarioSimulacao.Criar(
            nome: "Otimista",
            anoBase: 2026,
            criadoPor: "sub|abc123",
            clock: clock);

        // Assert
        cenario.CriadoPor.Should().Be("sub|abc123");
        cenario.CreatedAt.Should().Be(T0);
        cenario.UpdatedAt.Should().Be(T0);
    }

    // ── 5. Ativar_deRascunho_funciona ─────────────────────────────────────────

    [Fact]
    public void Ativar_deRascunho_funciona()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());

        // Act
        cenario.Ativar(ClockPadrao());

        // Assert
        cenario.Status.Should().Be(StatusCenarioSimulacao.Ativo);
    }

    // ── 6. Ativar_jaAtivo_lancaExcecao ───────────────────────────────────────

    [Fact]
    public void Ativar_jaAtivo_lancaExcecao()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());
        cenario.Ativar(ClockPadrao());

        // Act
        Action ato = () => cenario.Ativar(ClockPadrao());

        // Assert
        ato.Should().Throw<InvalidOperationException>();
    }

    // ── 7. Ativar_arquivado_lancaExcecao ─────────────────────────────────────

    [Fact]
    public void Ativar_arquivado_lancaExcecao()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());
        cenario.Ativar(ClockPadrao());
        cenario.Arquivar(ClockPadrao());

        // Act
        Action ato = () => cenario.Ativar(ClockPadrao());

        // Assert
        ato.Should().Throw<InvalidOperationException>();
    }

    // ── 8. Arquivar_deAtivo_funciona ──────────────────────────────────────────

    [Fact]
    public void Arquivar_deAtivo_funciona()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());
        cenario.Ativar(ClockPadrao());

        // Act
        cenario.Arquivar(ClockPadrao());

        // Assert
        cenario.Status.Should().Be(StatusCenarioSimulacao.Arquivado);
    }

    // ── 9. Arquivar_deRascunho_lancaExcecao ──────────────────────────────────

    [Fact]
    public void Arquivar_deRascunho_lancaExcecao()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());

        // Act
        Action ato = () => cenario.Arquivar(ClockPadrao());

        // Assert
        ato.Should().Throw<InvalidOperationException>();
    }

    // ── 10. AdicionarSimulacao_emRascunho_funciona ────────────────────────────

    [Fact]
    public void AdicionarSimulacao_emRascunho_funciona()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());

        // Act
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());

        // Assert
        cenario.Simulacoes.Should().HaveCount(1);
    }

    // ── 11. AdicionarSimulacao_emAtivo_funciona ───────────────────────────────

    [Fact]
    public void AdicionarSimulacao_emAtivo_funciona()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());
        cenario.Ativar(ClockPadrao());

        // Act — cenários ativos ainda aceitam simulações (SPEC §6.2)
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());

        // Assert
        cenario.Simulacoes.Should().HaveCount(2);
    }

    // ── 12. AdicionarSimulacao_emArquivado_lancaExcecao ──────────────────────

    [Fact]
    public void AdicionarSimulacao_emArquivado_lancaExcecao()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());
        cenario.Ativar(ClockPadrao());
        cenario.Arquivar(ClockPadrao());

        // Act
        Action ato = () => cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());

        // Assert
        ato.Should().Throw<InvalidOperationException>();
    }

    // ── 13. RemoverSimulacao_emArquivado_lancaExcecao ─────────────────────────

    [Fact]
    public void RemoverSimulacao_emArquivado_lancaExcecao()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());
        Guid simId = cenario.Simulacoes.First().Id;
        cenario.Ativar(ClockPadrao());
        cenario.Arquivar(ClockPadrao());

        // Act
        Action ato = () => cenario.RemoverSimulacao(simId, ClockPadrao());

        // Assert
        ato.Should().Throw<InvalidOperationException>();
    }

    // ── 14. AdicionarSimulacao_atualizaUpdatedAt ──────────────────────────────

    [Fact]
    public void AdicionarSimulacao_atualizaUpdatedAt()
    {
        // Arrange
        CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockEm(T0));
        Instant t1 = T0.Plus(Duration.FromHours(1));
        var clockLater = ClockEm(t1);

        // Act
        cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), clockLater);

        // Assert
        cenario.UpdatedAt.Should().Be(t1);
    }

    // ── 15. Property: cenário arquivado é imutável ────────────────────────────

    [Property(MaxTest = 50)]
    public Property Property_cenarioArquivado_eImutavel()
    {
        return Prop.ForAll(
            Arb.Default.Guid(),
            _ =>
            {
                // Arrange
                CenarioSimulacao cenario = CenarioSimulacao.Criar("X", 2026, "u", ClockPadrao());
                cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao());
                cenario.Ativar(ClockPadrao());
                cenario.Arquivar(ClockPadrao());

                // Assert — todas as operações de mutação lançam exceção
                bool ativarLanca = ThrowsInvalidOp(() => cenario.Ativar(ClockPadrao()));
                bool adicionarLanca = ThrowsInvalidOp(() => cenario.AdicionarSimulacao(CriarSimulacaoValida(cenario.Id), ClockPadrao()));

                return ativarLanca && adicionarLanca;
            });
    }

    // ── Helper estático para property test ───────────────────────────────────

    private static bool ThrowsInvalidOp(Action ato)
    {
        try
        {
            ato();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}

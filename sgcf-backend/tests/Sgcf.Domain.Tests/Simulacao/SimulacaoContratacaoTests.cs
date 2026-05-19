using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

using Xunit;

namespace Sgcf.Domain.Tests.Simulacao;

/// <summary>
/// Testes unitários para <see cref="SimulacaoContratacao"/>.
/// Cobre os invariantes I-1..I-11 definidos na SPEC §6.3.
/// </summary>
[Trait("Category", "Domain")]
public sealed class SimulacaoContratacaoTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Data "hoje" fixa para testes: 19-mai-2026.
    /// A data prevista de contratação deve ser >= esta data.
    /// </summary>
    private static readonly Instant Hoje = Instant.FromUtc(2026, 5, 19, 10, 0, 0);

    private static IClock ClockPadrao()
    {
        var clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Hoje);
        return clock;
    }

    /// <summary>
    /// Fábrica de simulação válida com defaults razoáveis.
    /// Substitua os parâmetros que deseja testar.
    /// </summary>
    private static SimulacaoContratacao CriarSimulacaoValida(
        Guid? cenarioId = null,
        Guid? bancoId = null,
        ModalidadeContrato modalidade = ModalidadeContrato.CapitalDeGiro,
        Moeda moeda = Moeda.Brl,
        decimal valorPrincipal = 1_000_000m,
        LocalDate? dataContratacao = null,
        LocalDate? dataPrimeiroVencimento = null,
        TipoTaxa tipoTaxa = TipoTaxa.CdiSpread,
        Percentual? taxaAa = null,
        Percentual? spreadAa = null,
        BaseCalculo baseCalculo = BaseCalculo.Dias252,
        EstruturaAmortizacao estrutura = EstruturaAmortizacao.Bullet,
        Periodicidade periodicidade = Periodicidade.Bullet,
        int quantidadeParcelas = 1,
        AnchorDiaMes anchorDiaMes = AnchorDiaMes.DiaFixo,
        int? anchorDiaFixo = 15,
        string? garantiaExigidaPrevista = null,
        string? observacoes = null,
        IClock? clock = null)
    {
        return SimulacaoContratacao.Criar(
            cenarioId: cenarioId ?? Guid.NewGuid(),
            bancoId: bancoId ?? Guid.NewGuid(),
            modalidade: modalidade,
            moeda: moeda,
            valorPrincipal: new Money(valorPrincipal, moeda),
            dataContratacaoPrevista: dataContratacao ?? new LocalDate(2026, 7, 15),
            dataPrimeiroVencimento: dataPrimeiroVencimento ?? new LocalDate(2026, 8, 15),
            tipoTaxa: tipoTaxa,
            taxaAa: taxaAa,
            spreadAa: spreadAa ?? Percentual.De(3m),  // CDI+spread padrão
            baseCalculo: baseCalculo,
            estruturaAmortizacao: estrutura,
            periodicidade: periodicidade,
            quantidadeParcelas: quantidadeParcelas,
            anchorDiaMes: anchorDiaMes,
            anchorDiaFixo: anchorDiaFixo,
            garantiaExigidaPrevista: garantiaExigidaPrevista,
            observacoes: observacoes,
            clock: clock ?? ClockPadrao());
    }

    // ── I-1: ValorPrincipal > 0 ───────────────────────────────────────────────

    [Fact]
    public void Criar_valida_ValorPrincipalPositivo()
    {
        // Valor zero deve lançar
        Action ato = () => CriarSimulacaoValida(valorPrincipal: 0m);
        ato.Should().Throw<ArgumentException>()
            .WithMessage("*principal*", because: "I-1: ValorPrincipal deve ser > 0");
    }

    // ── I-2: DataContratacaoPrevista >= hoje ──────────────────────────────────

    [Fact]
    public void Criar_valida_DataContratacaoPrevistaNoFuturoOuHoje()
    {
        // Ontem (2026-05-18) deve rejeitar
        Action ato = () => CriarSimulacaoValida(
            dataContratacao: new LocalDate(2026, 5, 18));

        ato.Should().Throw<ArgumentException>()
            .WithMessage("*contratacao*", because: "I-2: data no passado rejeitada");
    }

    // ── I-3: DataPrimeiroVencimento > DataContratacaoPrevista ─────────────────

    [Fact]
    public void Criar_valida_DataPrimeiroVencimentoPosteriorContratacao()
    {
        // Vencimento = contratação (não posterior)
        Action ato = () => CriarSimulacaoValida(
            dataContratacao: new LocalDate(2026, 7, 15),
            dataPrimeiroVencimento: new LocalDate(2026, 7, 15));

        ato.Should().Throw<ArgumentException>()
            .WithMessage("*vencimento*", because: "I-3: vencimento deve ser posterior à contratação");
    }

    // ── I-4: DataContratacaoPrevista dentro do AnoBase ────────────────────────

    [Fact]
    public void Criar_valida_DataContratacaoDentroDoAnoBase()
    {
        // AnoBase = 2026 mas data em 2027 deve rejeitar
        // Passamos anoBase via parâmetro direto para testar a invariante
        Action ato = () => SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.CapitalDeGiro,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2027, 3, 10),
            dataPrimeiroVencimento: new LocalDate(2027, 4, 10),
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
            clock: ClockPadrao(),
            anoBase: 2026);  // explícito: data 2027 fora do anoBase 2026

        ato.Should().Throw<ArgumentException>()
            .WithMessage("*ano base*", because: "I-4: data fora do anoBase rejeitada");
    }

    // ── I-5: QuantidadeParcelas >= 1 ─────────────────────────────────────────

    [Fact]
    public void Criar_valida_QuantidadeParcelasMinima()
    {
        Action ato = () => CriarSimulacaoValida(quantidadeParcelas: 0);

        ato.Should().Throw<ArgumentException>()
            .WithMessage("*parcelas*", because: "I-5: parcelas mínimas = 1");
    }

    // ── I-6: TipoTaxa.Fixa exige TaxaAa (SpreadAa = null) ───────────────────

    [Fact]
    public void Criar_TipoTaxaFixa_exigeTaxaAa()
    {
        // TipoTaxa.Fixa sem TaxaAa deve lançar
        Action semTaxa = () => CriarSimulacaoValida(
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: null,
            spreadAa: null);

        semTaxa.Should().Throw<ArgumentException>()
            .WithMessage("*taxa*", because: "I-6: Fixa exige TaxaAa");

        // TipoTaxa.Fixa com SpreadAa deve lançar (SpreadAa é exclusivo do CdiSpread)
        Action comSpread = () => CriarSimulacaoValida(
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(12m),
            spreadAa: Percentual.De(2m));

        comSpread.Should().Throw<ArgumentException>()
            .WithMessage("*spread*", because: "I-6: Fixa não aceita SpreadAa");
    }

    // ── I-7: TipoTaxa.CdiSpread exige SpreadAa e Moeda == BRL ────────────────

    [Fact]
    public void Criar_TipoTaxaCdiSpread_exigeSpread_e_MoedaBrl()
    {
        // CdiSpread sem SpreadAa deve lançar
        Action semSpread = () => CriarSimulacaoValida(
            tipoTaxa: TipoTaxa.CdiSpread,
            spreadAa: null,
            taxaAa: null);

        semSpread.Should().Throw<ArgumentException>()
            .WithMessage("*spread*", because: "I-7: CdiSpread exige SpreadAa");

        // CdiSpread com moeda USD deve lançar
        Action moedaErrada = () => CriarSimulacaoValida(
            modalidade: ModalidadeContrato.Finimp,
            moeda: Moeda.Usd,
            tipoTaxa: TipoTaxa.CdiSpread,
            spreadAa: Percentual.De(3m),
            taxaAa: null);

        moedaErrada.Should().Throw<ArgumentException>()
            .WithMessage("*CDI*", because: "I-7: CdiSpread só faz sentido em BRL");
    }

    // ── I-8: FINIMP não aceita BRL ────────────────────────────────────────────

    [Fact]
    public void Criar_modalidadeFinimp_naoAceitaBrl()
    {
        Action ato = () => CriarSimulacaoValida(
            modalidade: ModalidadeContrato.Finimp,
            moeda: Moeda.Brl,
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(8m),
            spreadAa: null);

        ato.Should().Throw<ArgumentException>()
            .WithMessage("*modalidade*", because: "I-8: FINIMP não aceita BRL");
    }

    // ── I-11: GarantiaExigidaPrevista — null ou <= 500 chars ─────────────────

    [Fact]
    public void Criar_GarantiaExigidaPrevista_aceitaNullOuAteh500Chars()
    {
        // null deve ser aceito
        Action nula = () => CriarSimulacaoValida(garantiaExigidaPrevista: null);
        nula.Should().NotThrow();

        // 500 chars exatos deve ser aceito
        Action exato = () => CriarSimulacaoValida(garantiaExigidaPrevista: new string('A', 500));
        exato.Should().NotThrow();
    }

    [Fact]
    public void Criar_GarantiaExigidaPrevista_acima500Chars_lancaExcecao()
    {
        // 501 chars deve lançar
        Action ato = () => CriarSimulacaoValida(garantiaExigidaPrevista: new string('A', 501));

        ato.Should().Throw<ArgumentException>()
            .WithMessage("*garantia*", because: "I-11: máx 500 chars");
    }

    // ── AD-3: Atualizar incrementa Version ───────────────────────────────────

    [Fact]
    public void Atualizar_incrementaVersion()
    {
        // Arrange
        SimulacaoContratacao sim = CriarSimulacaoValida();
        int versaoInicial = sim.Version;
        versaoInicial.Should().Be(1, because: "Version começa em 1 na criação");

        // Act
        sim.Atualizar(
            valorPrincipal: new Money(2_000_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 7, 15),
            dataPrimeiroVencimento: new LocalDate(2026, 8, 15),
            tipoTaxa: TipoTaxa.CdiSpread,
            taxaAa: null,
            spreadAa: Percentual.De(4m),
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Bullet,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaFixo,
            anchorDiaFixo: 15,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: ClockPadrao());

        // Assert
        sim.Version.Should().Be(versaoInicial + 1, because: "AD-3: cada mutação incrementa Version");
    }
}

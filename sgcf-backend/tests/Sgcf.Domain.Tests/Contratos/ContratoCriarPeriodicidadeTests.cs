using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

[Trait("Category", "Domain")]
public sealed class ContratoCriarPeriodicidadeTests
{
    private static IClock CriarClock() =>
        CriarClock(Instant.FromUtc(2026, 5, 14, 10, 0));

    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    // ── Teste 1: Defaults — sem periodicidade explícita, usa Bullet ─────────

    [Fact]
    public void Criar_ComCamposDefault_DefinePeriodicidadeBullet()
    {
        // Arrange
        IClock clock = CriarClock();

        // Act
        Contrato contrato = Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(500_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        // Assert
        contrato.Periodicidade.Should().Be(Periodicidade.Bullet);
        contrato.EstruturaAmortizacao.Should().Be(EstruturaAmortizacao.Bullet);
        contrato.QuantidadeParcelas.Should().Be(1);
        contrato.AnchorDiaMes.Should().Be(AnchorDiaMes.DiaContratacao);
        contrato.AnchorDiaFixo.Should().BeNull();
        contrato.PeriodicidadeJuros.Should().BeNull();
        contrato.DataPrimeiroVencimento.Should().Be(new LocalDate(2027, 1, 15));
    }

    // ── Teste 2: Mensal, 60 parcelas ────────────────────────────────────────

    [Fact]
    public void Criar_ComPeriodicidadeMensal_QuantidadeParcelas60_Sucesso()
    {
        // Arrange
        IClock clock = CriarClock();
        LocalDate dataContratacao = new LocalDate(2026, 1, 15);
        LocalDate dataVencimento = new LocalDate(2031, 1, 15);
        LocalDate dataPrimeiroVencimento = new LocalDate(2026, 2, 15);

        // Act
        Contrato contrato = Contrato.Criar(
            numeroExterno: "FIN-2026-002",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacao: dataContratacao,
            dataVencimento: dataVencimento,
            taxaAa: Percentual.De(7m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            periodicidade: Periodicidade.Mensal,
            estruturaAmortizacao: EstruturaAmortizacao.Price,
            quantidadeParcelas: 60,
            dataPrimeiroVencimento: dataPrimeiroVencimento);

        // Assert
        contrato.Periodicidade.Should().Be(Periodicidade.Mensal);
        contrato.EstruturaAmortizacao.Should().Be(EstruturaAmortizacao.Price);
        contrato.QuantidadeParcelas.Should().Be(60);
        contrato.DataPrimeiroVencimento.Should().Be(dataPrimeiroVencimento);
    }

    // ── Teste 3: QuantidadeParcelas = 0 lança ArgumentException ───────────

    [Fact]
    public void Criar_QuantidadeParcelasZero_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock();

        // Act
        Action act = () => Contrato.Criar(
            numeroExterno: "FIN-2026-003",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            quantidadeParcelas: 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("quantidadeParcelas");
    }

    // ── Teste 4: AnchorDiaFixo=15 com AnchorDiaMes=DiaFixo — sucesso ───────

    [Fact]
    public void Criar_AnchorDiaFixo15_ComAnchorDiaMesDiaFixo_Sucesso()
    {
        // Arrange
        IClock clock = CriarClock();

        // Act
        Contrato contrato = Contrato.Criar(
            numeroExterno: "FIN-2026-004",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(200_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 10),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            periodicidade: Periodicidade.Mensal,
            quantidadeParcelas: 12,
            dataPrimeiroVencimento: new LocalDate(2026, 2, 15),
            anchorDiaMes: AnchorDiaMes.DiaFixo,
            anchorDiaFixo: 15);

        // Assert
        contrato.AnchorDiaMes.Should().Be(AnchorDiaMes.DiaFixo);
        contrato.AnchorDiaFixo.Should().Be(15);
    }

    // ── Teste 5: AnchorDiaFixo informado com AnchorDiaMes!=DiaFixo — erro ──

    [Fact]
    public void Criar_AnchorDiaFixoInformado_ComAnchorDiaMesDiaContratacao_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock();

        // Act
        Action act = () => Contrato.Criar(
            numeroExterno: "FIN-2026-005",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: 15);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("anchorDiaFixo");
    }

    // ── Teste 6: DataPrimeiroVencimento anterior a DataContratacao — erro ───

    [Fact]
    public void Criar_DataPrimeiroVencimentoAnteriorDataContratacao_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock();
        LocalDate dataContratacao = new LocalDate(2026, 6, 1);
        LocalDate dataPrimeiroVencimentoAnterior = new LocalDate(2026, 5, 1);

        // Act
        Action act = () => Contrato.Criar(
            numeroExterno: "FIN-2026-006",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: dataContratacao,
            dataVencimento: new LocalDate(2027, 6, 1),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            periodicidade: Periodicidade.Mensal,
            quantidadeParcelas: 12,
            dataPrimeiroVencimento: dataPrimeiroVencimentoAnterior);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("dataPrimeiroVencimento");
    }

    // ── Teste extra: AnchorDiaFixo=DiaFixo mas valor fora de 1-31 — erro ───

    [Fact]
    public void Criar_AnchorDiaFixoFora1A31_ComAnchorDiaMesDiaFixo_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock();

        // Act
        Action act = () => Contrato.Criar(
            numeroExterno: "FIN-2026-007",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock,
            anchorDiaMes: AnchorDiaMes.DiaFixo,
            anchorDiaFixo: 32);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("anchorDiaFixo");
    }
}

using System.Linq;

using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

[Trait("Category", "Domain")]
public sealed class ContratoTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static Contrato CriarContratoValido(IClock clock)
    {
        return Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    // ── Criar — happy path ──────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        Instant agora = Instant.FromUtc(2026, 5, 11, 10, 0);
        IClock clock = CriarClock(agora);
        Guid bancoId = Guid.NewGuid();

        // Act
        Contrato contrato = Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        // Assert
        contrato.NumeroExterno.Should().Be("FIN-2026-001");
        contrato.BancoId.Should().Be(bancoId);
        contrato.Modalidade.Should().Be(ModalidadeContrato.Finimp);
        contrato.Status.Should().Be(StatusContrato.Ativo);
        contrato.Moeda.Should().Be(Moeda.Usd);
        contrato.ValorPrincipal.Valor.Should().Be(1_000_000m);
        contrato.ValorPrincipal.Moeda.Should().Be(Moeda.Usd);
        contrato.DataContratacao.Should().Be(new LocalDate(2026, 1, 15));
        contrato.DataVencimento.Should().Be(new LocalDate(2027, 1, 15));
        contrato.TaxaAa.AsHumano.Should().Be(6m);
        contrato.BaseCalculo.Should().Be(BaseCalculo.Dias360);
        contrato.CreatedAt.Should().Be(agora);
        contrato.UpdatedAt.Should().Be(agora);
        contrato.DeletedAt.Should().BeNull();
        contrato.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Criar_StatusInicialEhAtivo()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Contrato contrato = CriarContratoValido(clock);

        // Assert
        contrato.Status.Should().Be(StatusContrato.Ativo);
    }

    [Fact]
    public void Criar_MoedaDerivadaDeValorPrincipal()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Contrato contrato = Contrato.Criar(
            numeroExterno: "FIN-2026-002",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Lei4131,
            valorPrincipal: new Money(500_000m, Moeda.Eur),
            dataContratacao: new LocalDate(2026, 3, 1),
            dataVencimento: new LocalDate(2028, 3, 1),
            taxaAa: Percentual.De(4m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        // Assert
        contrato.Moeda.Should().Be(Moeda.Eur);
    }

    // ── Criar — guard: dataVencimento deve ser posterior a dataContratacao ──

    [Fact]
    public void Criar_DataVencimentoIgualADataContratacao_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));
        LocalDate mesmaData = new LocalDate(2026, 6, 1);

        // Act
        Action act = () => Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: mesmaData,
            dataVencimento: mesmaData,
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("dataVencimento");
    }

    [Fact]
    public void Criar_DataVencimentoAnteriorADataContratacao_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Action act = () => Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 6, 1),
            dataVencimento: new LocalDate(2026, 5, 1),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("dataVencimento");
    }

    // ── Criar — guard: numeroExterno vazio ──────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NumeroExternoVazioOuWhitespace_LancaArgumentException(string numeroVazio)
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Action act = () => Contrato.Criar(
            numeroExterno: numeroVazio,
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("numeroExterno");
    }

    // ── Liquidar ─────────────────────────────────────────────────────────────

    [Fact]
    public void Liquidar_AlteraStatusParaLiquidado_EAvancaUpdatedAt()
    {
        // Arrange
        Instant criacaoInstant = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant liquidacaoInstant = Instant.FromUtc(2026, 7, 15, 14, 0);

        Contrato contrato = CriarContratoValido(CriarClock(criacaoInstant));

        // Act
        contrato.Liquidar(CriarClock(liquidacaoInstant));

        // Assert
        contrato.Status.Should().Be(StatusContrato.Liquidado);
        contrato.UpdatedAt.Should().Be(liquidacaoInstant);
    }

    // ── MarcarVencido ────────────────────────────────────────────────────────

    [Fact]
    public void MarcarVencido_AlteraStatusParaVencido()
    {
        // Arrange
        Instant criacaoInstant = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant vencimentoInstant = Instant.FromUtc(2027, 1, 16, 0, 0);

        Contrato contrato = CriarContratoValido(CriarClock(criacaoInstant));

        // Act
        contrato.MarcarVencido(CriarClock(vencimentoInstant));

        // Assert
        contrato.Status.Should().Be(StatusContrato.Vencido);
        contrato.UpdatedAt.Should().Be(vencimentoInstant);
    }

    // ── Deletar ──────────────────────────────────────────────────────────────

    [Fact]
    public void Deletar_DefineDeletedAt_StatusNaoSeMuda()
    {
        // Arrange
        Instant criacaoInstant = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant exclusaoInstant = Instant.FromUtc(2026, 5, 20, 9, 0);

        Contrato contrato = CriarContratoValido(CriarClock(criacaoInstant));

        // Act
        contrato.Deletar(CriarClock(exclusaoInstant));

        // Assert
        contrato.DeletedAt.Should().Be(exclusaoInstant);
        contrato.Status.Should().Be(StatusContrato.Ativo);
    }

    // ── AdicionarParcela ─────────────────────────────────────────────────────

    [Fact]
    public void AdicionarParcela_ComDadosValidos_ParcelaEstaEmParcelas()
    {
        // Arrange
        Contrato contrato = CriarContratoValido(CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0)));

        // Act
        contrato.AdicionarParcela(
            numero: 1,
            dataVencimento: new LocalDate(2026, 7, 15),
            valorPrincipal: new Money(500_000m, Moeda.Usd),
            valorJuros: new Money(5_000m, Moeda.Usd));

        // Assert
        contrato.Parcelas.Should().HaveCount(1);
        Parcela parcela = contrato.Parcelas.First();
        parcela.Numero.Should().Be(1);
        parcela.ValorPrincipal.Valor.Should().Be(500_000m);
        parcela.ValorJuros.Valor.Should().Be(5_000m);
        parcela.DataVencimento.Should().Be(new LocalDate(2026, 7, 15));
    }

    [Fact]
    public void AdicionarParcela_MoedaDiferenteDaDoContrato_LancaArgumentException()
    {
        // Arrange
        Contrato contrato = CriarContratoValido(CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0)));

        // Act — contrato é USD, parcela em BRL
        Action act = () => contrato.AdicionarParcela(
            numero: 1,
            dataVencimento: new LocalDate(2026, 7, 15),
            valorPrincipal: new Money(500_000m, Moeda.Brl),
            valorJuros: new Money(5_000m, Moeda.Usd));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AdicionarParcela_MoedaJurosDiferenteDaDoContrato_LancaArgumentException()
    {
        // Arrange
        Contrato contrato = CriarContratoValido(CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0)));

        // Act — juros em BRL mas contrato é USD
        Action act = () => contrato.AdicionarParcela(
            numero: 1,
            dataVencimento: new LocalDate(2026, 7, 15),
            valorPrincipal: new Money(500_000m, Moeda.Usd),
            valorJuros: new Money(5_000m, Moeda.Brl));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Garantia.Criar — testes de domínio (garantias são criadas diretamente, não via Contrato) ──

    [Fact]
    public void GarantiasCriar_ComDadosValidos_GarantiaTemPropriedadesCorretas()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));
        Guid contratoId = Guid.NewGuid();

        // Act
        Garantia garantia = Garantia.Criar(
            contratoId: contratoId,
            tipo: TipoGarantia.CdbCativo,
            valorBrl: new Money(200_000m, Moeda.Brl),
            principalBrlParaCalculo: 1_000_000m,
            dataConstituicao: new LocalDate(2026, 1, 15),
            dataLiberacaoPrevista: null,
            observacoes: null,
            createdBy: "test",
            clock: clock);

        // Assert
        garantia.Tipo.Should().Be(TipoGarantia.CdbCativo);
        garantia.ValorBrl.Valor.Should().Be(200_000m);
        garantia.ValorBrl.Moeda.Should().Be(Moeda.Brl);
        garantia.DataConstituicao.Should().Be(new LocalDate(2026, 1, 15));
        garantia.Status.Should().Be(StatusGarantia.Ativa);
        garantia.PercentualPrincipal.Should().NotBeNull();
        garantia.PercentualPrincipal!.Value.AsHumano.Should().Be(20m);
    }
}

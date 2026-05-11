using System.Linq;

using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

[Trait("Category", "Domain")]
public sealed class ParcelaTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    /// <summary>
    /// Cria um contrato USD e adiciona uma parcela de 500.000 principal + 5.000 juros,
    /// retornando a parcela para uso nos testes.
    /// </summary>
    private static Parcela CriarParcelaPendente()
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        Contrato contrato = Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        contrato.AdicionarParcela(
            numero: 1,
            dataVencimento: new LocalDate(2026, 7, 15),
            valorPrincipal: new Money(500_000m, Moeda.Usd),
            valorJuros: new Money(5_000m, Moeda.Usd));

        return contrato.Parcelas.First();
    }

    // ── Estado inicial ───────────────────────────────────────────────────────

    [Fact]
    public void EstadoInicial_StatusEhPendente()
    {
        // Arrange & Act
        Parcela parcela = CriarParcelaPendente();

        // Assert
        parcela.Status.Should().Be(StatusParcela.Pendente);
    }

    [Fact]
    public void EstadoInicial_ValorPagoEhNull()
    {
        // Arrange & Act
        Parcela parcela = CriarParcelaPendente();

        // Assert
        parcela.ValorPago.Should().BeNull();
    }

    [Fact]
    public void EstadoInicial_DataPagamentoEhNull()
    {
        // Arrange & Act
        Parcela parcela = CriarParcelaPendente();

        // Assert
        parcela.DataPagamento.Should().BeNull();
    }

    [Fact]
    public void EstadoInicial_PropriedadesDeValorEstaoCorretas()
    {
        // Arrange & Act
        Parcela parcela = CriarParcelaPendente();

        // Assert
        parcela.ValorPrincipal.Valor.Should().Be(500_000m);
        parcela.ValorJuros.Valor.Should().Be(5_000m);
        parcela.Moeda.Should().Be(Moeda.Usd);
        parcela.Id.Should().NotBeEmpty();
    }

    // ── RegistrarPagamento — pagamento total ─────────────────────────────────

    [Fact]
    public void RegistrarPagamento_ValorIgualAoTotal_StatusEhPaga()
    {
        // Arrange
        Parcela parcela = CriarParcelaPendente();
        // Total = 500.000 + 5.000 = 505.000
        Money pagamentoTotal = new Money(505_000m, Moeda.Usd);
        LocalDate dataPagamento = new LocalDate(2026, 7, 15);

        // Act
        parcela.RegistrarPagamento(pagamentoTotal, dataPagamento);

        // Assert
        parcela.Status.Should().Be(StatusParcela.Paga);
        parcela.ValorPago.Should().NotBeNull();
        parcela.ValorPago!.Value.Valor.Should().Be(505_000m);
        parcela.DataPagamento.Should().Be(dataPagamento);
    }

    [Fact]
    public void RegistrarPagamento_ValorMaiorQueTotal_StatusEhPaga()
    {
        // Arrange
        Parcela parcela = CriarParcelaPendente();
        // Total = 505.000, paga 510.000 (com multa/outros)
        Money pagamentoExcedente = new Money(510_000m, Moeda.Usd);

        // Act
        parcela.RegistrarPagamento(pagamentoExcedente, new LocalDate(2026, 7, 15));

        // Assert
        parcela.Status.Should().Be(StatusParcela.Paga);
    }

    // ── RegistrarPagamento — pagamento parcial ───────────────────────────────

    [Fact]
    public void RegistrarPagamento_ValorMenorQueTotal_StatusEhParcial()
    {
        // Arrange
        Parcela parcela = CriarParcelaPendente();
        // Total = 505.000, paga apenas 300.000
        Money pagamentoParcial = new Money(300_000m, Moeda.Usd);
        LocalDate dataPagamento = new LocalDate(2026, 7, 15);

        // Act
        parcela.RegistrarPagamento(pagamentoParcial, dataPagamento);

        // Assert
        parcela.Status.Should().Be(StatusParcela.Parcial);
        parcela.ValorPago.Should().NotBeNull();
        parcela.ValorPago!.Value.Valor.Should().Be(300_000m);
        parcela.DataPagamento.Should().Be(dataPagamento);
    }

    // ── RegistrarPagamento — guards ──────────────────────────────────────────

    [Fact]
    public void RegistrarPagamento_MoedaDiferente_LancaArgumentException()
    {
        // Arrange
        Parcela parcela = CriarParcelaPendente();
        Money pagamentoEmMoedaErrada = new Money(505_000m, Moeda.Brl);

        // Act
        Action act = () => parcela.RegistrarPagamento(pagamentoEmMoedaErrada, new LocalDate(2026, 7, 15));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegistrarPagamento_ValorZero_LancaArgumentException()
    {
        // Arrange
        Parcela parcela = CriarParcelaPendente();
        Money pagamentoZero = new Money(0m, Moeda.Usd);

        // Act
        Action act = () => parcela.RegistrarPagamento(pagamentoZero, new LocalDate(2026, 7, 15));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegistrarPagamento_ValorNegativo_LancaArgumentException()
    {
        // Arrange — Money não aceita valores negativos sem explicitamente testar isso
        // Verificamos que a guard do RegistrarPagamento recusa valor <= 0
        Parcela parcela = CriarParcelaPendente();

        // Act — tentamos um valor negativo via reflexão interna; usamos Money com valor negativo
        // O construtor de Money não valida negativo, mas RegistrarPagamento sim
        Money pagamentoNegativo = new Money(-1m, Moeda.Usd);
        Action act = () => parcela.RegistrarPagamento(pagamentoNegativo, new LocalDate(2026, 7, 15));

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

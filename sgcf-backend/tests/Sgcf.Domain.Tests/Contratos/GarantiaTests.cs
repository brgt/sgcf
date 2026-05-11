using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Domain.Tests.Contratos;

[Trait("Category", "Domain")]
public sealed class GarantiaTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static Garantia CriarGarantiaValida(
        IClock clock,
        TipoGarantia tipo = TipoGarantia.CdbCativo,
        decimal valorBrl = 200_000m,
        decimal? principalBrl = 1_000_000m)
    {
        return Garantia.Criar(
            contratoId: Guid.NewGuid(),
            tipo: tipo,
            valorBrl: new Money(valorBrl, Moeda.Brl),
            principalBrlParaCalculo: principalBrl,
            dataConstituicao: new LocalDate(2026, 1, 15),
            dataLiberacaoPrevista: null,
            observacoes: null,
            createdBy: "test-user",
            clock: clock);
    }

    // ── Criar — happy path ────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DefinePropriedadesCorretas()
    {
        // Arrange
        Instant agora = Instant.FromUtc(2026, 5, 11, 10, 0);
        IClock clock = CriarClock(agora);
        Guid contratoId = Guid.NewGuid();

        // Act
        Garantia garantia = Garantia.Criar(
            contratoId: contratoId,
            tipo: TipoGarantia.Aval,
            valorBrl: new Money(500_000m, Moeda.Brl),
            principalBrlParaCalculo: 1_000_000m,
            dataConstituicao: new LocalDate(2026, 3, 1),
            dataLiberacaoPrevista: new LocalDate(2027, 3, 1),
            observacoes: "Aval do sócio",
            createdBy: "admin",
            clock: clock);

        // Assert
        garantia.ContratoId.Should().Be(contratoId);
        garantia.Tipo.Should().Be(TipoGarantia.Aval);
        garantia.ValorBrl.Valor.Should().Be(500_000m);
        garantia.ValorBrl.Moeda.Should().Be(Moeda.Brl);
        garantia.DataConstituicao.Should().Be(new LocalDate(2026, 3, 1));
        garantia.DataLiberacaoPrevista.Should().Be(new LocalDate(2027, 3, 1));
        garantia.DataLiberacaoEfetiva.Should().BeNull();
        garantia.Status.Should().Be(StatusGarantia.Ativa);
        garantia.Observacoes.Should().Be("Aval do sócio");
        garantia.CreatedBy.Should().Be("admin");
        garantia.CreatedAt.Should().Be(agora);
        garantia.UpdatedAt.Should().Be(agora);
        garantia.Id.Should().NotBeEmpty();
    }

    // ── Criar — PercentualPrincipal calculado corretamente ───────────────────

    [Fact]
    public void Criar_GarantiaComValorBrl_ComputaPercentualCorreto()
    {
        // Arrange — garantia de 200k sobre principal de 1M = 20%
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Garantia garantia = Garantia.Criar(
            contratoId: Guid.NewGuid(),
            tipo: TipoGarantia.CdbCativo,
            valorBrl: new Money(200_000m, Moeda.Brl),
            principalBrlParaCalculo: 1_000_000m,
            dataConstituicao: new LocalDate(2026, 1, 15),
            dataLiberacaoPrevista: null,
            observacoes: null,
            createdBy: "test",
            clock: clock);

        // Assert
        garantia.PercentualPrincipal.Should().NotBeNull();
        garantia.PercentualPrincipal!.Value.AsHumano.Should().Be(20m);
    }

    [Fact]
    public void Criar_PrincipalNulo_PercentualPrincipalNulo()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Garantia garantia = Garantia.Criar(
            contratoId: Guid.NewGuid(),
            tipo: TipoGarantia.Sblc,
            valorBrl: new Money(300_000m, Moeda.Brl),
            principalBrlParaCalculo: null,
            dataConstituicao: new LocalDate(2026, 2, 1),
            dataLiberacaoPrevista: null,
            observacoes: null,
            createdBy: "test",
            clock: clock);

        // Assert
        garantia.PercentualPrincipal.Should().BeNull();
    }

    [Fact]
    public void Criar_PrincipalZero_PercentualPrincipalNulo()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Garantia garantia = Garantia.Criar(
            contratoId: Guid.NewGuid(),
            tipo: TipoGarantia.Aval,
            valorBrl: new Money(100_000m, Moeda.Brl),
            principalBrlParaCalculo: 0m,
            dataConstituicao: new LocalDate(2026, 1, 1),
            dataLiberacaoPrevista: null,
            observacoes: null,
            createdBy: "test",
            clock: clock);

        // Assert
        garantia.PercentualPrincipal.Should().BeNull();
    }

    // ── Criar — guard: valor em moeda diferente de BRL ───────────────────────

    [Fact]
    public void Criar_ValorEmMoedaDiferenteDeBrl_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));

        // Act
        Action act = () => Garantia.Criar(
            contratoId: Guid.NewGuid(),
            tipo: TipoGarantia.Sblc,
            valorBrl: new Money(100_000m, Moeda.Usd),
            principalBrlParaCalculo: null,
            dataConstituicao: new LocalDate(2026, 1, 15),
            dataLiberacaoPrevista: null,
            observacoes: null,
            createdBy: "test",
            clock: clock);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("valorBrl");
    }

    // ── Criar — guard: dataLiberacaoPrevista deve ser posterior ──────────────

    [Fact]
    public void Criar_DataLiberacaoPrevistaIgualAConstituicao_LancaArgumentException()
    {
        // Arrange
        IClock clock = CriarClock(Instant.FromUtc(2026, 5, 11, 10, 0));
        LocalDate mesmaData = new(2026, 6, 1);

        // Act
        Action act = () => Garantia.Criar(
            contratoId: Guid.NewGuid(),
            tipo: TipoGarantia.Aval,
            valorBrl: new Money(100_000m, Moeda.Brl),
            principalBrlParaCalculo: null,
            dataConstituicao: mesmaData,
            dataLiberacaoPrevista: mesmaData,
            observacoes: null,
            createdBy: "test",
            clock: clock);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("dataLiberacaoPrevista");
    }

    // ── Liberar ───────────────────────────────────────────────────────────────

    [Fact]
    public void Liberar_SetaStatusLiberadaEDataEfetiva()
    {
        // Arrange
        Instant criacao = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant liberacao = Instant.FromUtc(2027, 1, 20, 9, 0);

        Garantia garantia = CriarGarantiaValida(CriarClock(criacao));

        // Act
        garantia.Liberar(new LocalDate(2027, 1, 20), CriarClock(liberacao));

        // Assert
        garantia.Status.Should().Be(StatusGarantia.Liberada);
        garantia.DataLiberacaoEfetiva.Should().Be(new LocalDate(2027, 1, 20));
        garantia.UpdatedAt.Should().Be(liberacao);
    }

    // ── Executar ──────────────────────────────────────────────────────────────

    [Fact]
    public void Executar_SetaStatusExecutada()
    {
        // Arrange
        Instant criacao = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant execucao = Instant.FromUtc(2026, 9, 1, 12, 0);

        Garantia garantia = CriarGarantiaValida(CriarClock(criacao));

        // Act
        garantia.Executar(CriarClock(execucao));

        // Assert
        garantia.Status.Should().Be(StatusGarantia.Executada);
        garantia.UpdatedAt.Should().Be(execucao);
    }

    // ── Cancelar ──────────────────────────────────────────────────────────────

    [Fact]
    public void Cancelar_SetaStatusCancelada()
    {
        // Arrange
        Instant criacao = Instant.FromUtc(2026, 5, 11, 10, 0);
        Instant cancelamento = Instant.FromUtc(2026, 6, 15, 8, 0);

        Garantia garantia = CriarGarantiaValida(CriarClock(criacao));

        // Act
        garantia.Cancelar(CriarClock(cancelamento));

        // Assert
        garantia.Status.Should().Be(StatusGarantia.Cancelada);
        garantia.UpdatedAt.Should().Be(cancelamento);
    }
}

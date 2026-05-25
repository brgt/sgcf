using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes unitários de <see cref="CalculadorValorGarantiaExigida"/>.
/// Cobre: coleção vazia, percentual puro, valor fixo puro, mix, Aval sem contribuição.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CalculadorValorGarantiaExigidaTests
{
    private static readonly Guid LimiteId = Guid.NewGuid();
    private static readonly Money ValorAlvo = new(1_000_000m, Moeda.Brl);

    private static readonly IClock Clock = CriarClock();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 16, 9, 0));
        return clock;
    }

    private static GarantiaExigidaItem CriarGarantia(
        TipoGarantia tipo,
        decimal? percentual = null,
        decimal? valorFixo = null,
        bool obrigatoria = true) =>
        GarantiaExigidaItem.Criar(
            revisaoId: LimiteId,
            tipo: tipo,
            percentualSobreLimite: percentual,
            valorFixoBrl: valorFixo.HasValue ? new Money(valorFixo.Value, Moeda.Brl) : null,
            obrigatoria: obrigatoria,
            observacoes: null,
            clock: Clock);

    // ─── Coleção vazia ────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_ColecaoVazia_RetornaZero()
    {
        Money resultado = CalculadorValorGarantiaExigida.Calcular(
            Array.Empty<GarantiaExigidaItem>(), ValorAlvo);

        resultado.Valor.Should().Be(0m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    // ─── Percentual puro ──────────────────────────────────────────────────────

    [Fact]
    public void Calcular_GarantiaComPercentual20PctSobreUmMilhao_Retorna200k()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m);

        Money resultado = CalculadorValorGarantiaExigida.Calcular([garantia], ValorAlvo);

        resultado.Valor.Should().Be(200_000m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    // ─── Valor fixo puro ──────────────────────────────────────────────────────

    [Fact]
    public void Calcular_GarantiaComValorFixo50k_Retorna50k()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.Sblc, valorFixo: 50_000m);

        Money resultado = CalculadorValorGarantiaExigida.Calcular([garantia], ValorAlvo);

        resultado.Valor.Should().Be(50_000m);
        resultado.Moeda.Should().Be(Moeda.Brl);
    }

    // ─── Mix: percentual + valor fixo ─────────────────────────────────────────

    [Fact]
    public void Calcular_CdbCativo20PctMaisSblc50k_RetornaSoma()
    {
        // 20% de 1.000.000 = 200.000 + 50.000 = 250.000
        IReadOnlyCollection<GarantiaExigidaItem> garantias =
        [
            CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m),
            CriarGarantia(TipoGarantia.Sblc,      valorFixo:  50_000m)
        ];

        Money resultado = CalculadorValorGarantiaExigida.Calcular(garantias, ValorAlvo);

        resultado.Valor.Should().Be(250_000m);
    }

    // ─── Aval não contribui com valor ────────────────────────────────────────

    [Fact]
    public void Calcular_ApenasAval_RetornaZero()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.Aval);

        Money resultado = CalculadorValorGarantiaExigida.Calcular([garantia], ValorAlvo);

        resultado.Valor.Should().Be(0m);
    }

    [Fact]
    public void Calcular_CdbCativo20PctMaisAval_IgnoraContribuicaoAval()
    {
        // Aval não adiciona valor; total deve ser apenas 20% do valorAlvo
        IReadOnlyCollection<GarantiaExigidaItem> garantias =
        [
            CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m),
            CriarGarantia(TipoGarantia.Aval)
        ];

        Money resultado = CalculadorValorGarantiaExigida.Calcular(garantias, ValorAlvo);

        resultado.Valor.Should().Be(200_000m);
    }

    // ─── Moeda errada → exceção clara ─────────────────────────────────────────

    [Fact]
    public void Calcular_ValorAlvoNaoBrl_LancaArgumentException()
    {
        GarantiaExigidaItem garantia = CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m);
        Money valorUsd = new(100_000m, Moeda.Usd);

        Action act = () => CalculadorValorGarantiaExigida.Calcular([garantia], valorUsd);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }
}

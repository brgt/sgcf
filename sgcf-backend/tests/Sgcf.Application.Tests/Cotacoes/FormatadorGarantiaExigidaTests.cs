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
/// Testes unitários de <see cref="FormatadorGarantiaExigida"/>.
/// Cada cenário corresponde a um exemplo da SPEC Task 4.1.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FormatadorGarantiaExigidaTests
{
    private static readonly Guid LimiteId = Guid.NewGuid();
    private static readonly IClock Clock = CriarClock();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 16, 9, 0));
        return clock;
    }

    private static GarantiaExigidaLimite CriarGarantia(
        TipoGarantia tipo,
        decimal? percentual = null,
        decimal? valorFixo = null,
        bool obrigatoria = true) =>
        GarantiaExigidaLimite.Criar(
            limiteBancoId: LimiteId,
            tipo: tipo,
            percentualSobreLimite: percentual,
            valorFixoBrl: valorFixo.HasValue ? new Money(valorFixo.Value, Moeda.Brl) : null,
            obrigatoria: obrigatoria,
            observacoes: null,
            clock: Clock);

    // ─── Coleção vazia ────────────────────────────────────────────────────────

    [Fact]
    public void Formatar_ColecaoVazia_RetornaStringVazia()
    {
        string resultado = FormatadorGarantiaExigida.Formatar(
            Array.Empty<GarantiaExigidaLimite>());

        resultado.Should().BeEmpty();
    }

    // ─── 1 item — CdbCativo 20% obrigatório ──────────────────────────────────

    [Fact]
    public void Formatar_CdbCativo20PctObrigatorio_RetornaStringCorreta()
    {
        GarantiaExigidaLimite garantia = CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m, obrigatoria: true);

        string resultado = FormatadorGarantiaExigida.Formatar([garantia]);

        resultado.Should().Be("CDB cativo 20% (obrigatório)");
    }

    // ─── 1 item — Aval sem valores obrigatório ────────────────────────────────

    [Fact]
    public void Formatar_AvalSemValoresObrigatorio_RetornaStringCorreta()
    {
        GarantiaExigidaLimite garantia = CriarGarantia(TipoGarantia.Aval, obrigatoria: true);

        string resultado = FormatadorGarantiaExigida.Formatar([garantia]);

        resultado.Should().Be("Aval (obrigatório)");
    }

    // ─── 1 item — FGI 10% opcional ────────────────────────────────────────────

    [Fact]
    public void Formatar_Fgi10PctOpcional_RetornaStringCorreta()
    {
        GarantiaExigidaLimite garantia = CriarGarantia(TipoGarantia.Fgi, percentual: 10m, obrigatoria: false);

        string resultado = FormatadorGarantiaExigida.Formatar([garantia]);

        resultado.Should().Be("FGI 10% (opcional)");
    }

    // ─── 1 item — SBLC R$ 200.000,00 obrigatório ─────────────────────────────

    [Fact]
    public void Formatar_Sblc200kBrlObrigatorio_RetornaStringCorreta()
    {
        GarantiaExigidaLimite garantia = CriarGarantia(TipoGarantia.Sblc, valorFixo: 200_000m, obrigatoria: true);

        string resultado = FormatadorGarantiaExigida.Formatar([garantia]);

        resultado.Should().Be("SBLC R$ 200.000,00 (obrigatório)");
    }

    // ─── 2 itens — CdbCativo 20% + Aval ──────────────────────────────────────

    [Fact]
    public void Formatar_CdbCativo20PctMaisAval_RetornaStringComSeparador()
    {
        IReadOnlyCollection<GarantiaExigidaLimite> garantias =
        [
            CriarGarantia(TipoGarantia.CdbCativo, percentual: 20m, obrigatoria: true),
            CriarGarantia(TipoGarantia.Aval, obrigatoria: true)
        ];

        string resultado = FormatadorGarantiaExigida.Formatar(garantias);

        resultado.Should().Be("CDB cativo 20% (obrigatório) + Aval (obrigatório)");
    }

    // ─── Traduções dos demais tipos ───────────────────────────────────────────

    [Theory]
    [InlineData(TipoGarantia.AlienacaoFiduciaria, "Alienação Fiduciária")]
    [InlineData(TipoGarantia.Duplicatas,          "Duplicatas")]
    [InlineData(TipoGarantia.RecebiveisCartao,    "Recebíveis de cartão")]
    [InlineData(TipoGarantia.BoletoBancario,      "Boleto bancário")]
    public void Formatar_TiposComPercentual_RetornaRotuloCorreto(TipoGarantia tipo, string rotuloEsperado)
    {
        GarantiaExigidaLimite garantia = CriarGarantia(tipo, percentual: 10m, obrigatoria: true);

        string resultado = FormatadorGarantiaExigida.Formatar([garantia]);

        resultado.Should().StartWith(rotuloEsperado);
    }

    // ─── Percentual decimal (não inteiro) ─────────────────────────────────────

    [Fact]
    public void Formatar_PercentualDecimal_ExibeComVirgulaPortuguesa()
    {
        GarantiaExigidaLimite garantia = CriarGarantia(TipoGarantia.CdbCativo, percentual: 12.5m, obrigatoria: true);

        string resultado = FormatadorGarantiaExigida.Formatar([garantia]);

        // pt-BR: vírgula como separador decimal
        resultado.Should().Be("CDB cativo 12,5% (obrigatório)");
    }
}

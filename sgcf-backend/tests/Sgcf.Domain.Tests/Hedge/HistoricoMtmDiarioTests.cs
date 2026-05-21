using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Hedge;
using Xunit;

namespace Sgcf.Domain.Tests.Hedge;

[Trait("Category", "Domain")]
public sealed class HistoricoMtmDiarioTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 21, 12, 0, 0);
    private static readonly LocalDate DataFixa    = new(2026, 5, 21);
    private static readonly Guid HedgeIdFixo      = Guid.NewGuid();

    // ── Fábrica happy path ────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComParametrosValidos_ArmazenaTodasAsPropriedades()
    {
        var historico = HistoricoMtmDiario.Criar(
            hedgeId:      HedgeIdFixo,
            data:         DataFixa,
            payoffBrl:    1_000m,
            spot:         5.25m,
            tipoCotacao:  "SPOT_INTRADAY",
            registradoEm: InstanteFixo);

        historico.HedgeId.Should().Be(HedgeIdFixo);
        historico.DataReferencia.Should().Be(DataFixa);
        historico.PayoffBrlDecimal.Should().Be(1_000m);
        historico.PayoffBrl.Valor.Should().Be(1_000m);
        historico.SpotUtilizado.Should().Be(5.25m);
        historico.TipoCotacao.Should().Be("SPOT_INTRADAY");
        historico.RegistradoEm.Should().Be(InstanteFixo);
    }

    // ── Posicao derivada do sinal do payoff ───────────────────────────────────

    [Fact]
    public void PayoffPositivo_Posicao_DeveSerReceber()
    {
        var h = CriarComPayoff(500m);
        DerivarPosicao(h).Should().Be("RECEBER");
    }

    [Fact]
    public void PayoffNegativo_Posicao_DeveSerPagar()
    {
        var h = CriarComPayoff(-200m);
        DerivarPosicao(h).Should().Be("PAGAR");
    }

    [Fact]
    public void PayoffZero_Posicao_DeveSerNeutro()
    {
        var h = CriarComPayoff(0m);
        DerivarPosicao(h).Should().Be("NEUTRO");
    }

    // ── Validação de TipoCotacao ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_TipoCotacaoVazio_DeveLancarArgumentException(string tipoCotacaoInvalido)
    {
        Action act = () => HistoricoMtmDiario.Criar(
            HedgeIdFixo, DataFixa, 100m, 5.0m, tipoCotacaoInvalido, InstanteFixo);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tipoCotacao");
    }

    [Fact]
    public void Criar_TipoCotacaoComMaisde30Chars_DeveLancarArgumentException()
    {
        string tipoCotacaoLongo = new string('X', 31);

        Action act = () => HistoricoMtmDiario.Criar(
            HedgeIdFixo, DataFixa, 100m, 5.0m, tipoCotacaoLongo, InstanteFixo);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("tipoCotacao");
    }

    // ── Validação de spot ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_SpotNaoPositivo_DeveLancarArgumentException(decimal spotInvalido)
    {
        Action act = () => HistoricoMtmDiario.Criar(
            HedgeIdFixo, DataFixa, 100m, spotInvalido, "SPOT_INTRADAY", InstanteFixo);

        act.Should().Throw<ArgumentException>();
    }

    // ── Mutador Atualizar ─────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ComValoresValidos_AlteraPropriedades()
    {
        var h = CriarComPayoff(100m);
        Instant novoInstante = Instant.FromUtc(2026, 5, 22, 9, 0, 0);

        h.Atualizar(novoPayoffBrl: -50m, novoSpot: 5.50m, novoTipoCotacao: "PTAX_D1", agora: novoInstante);

        h.PayoffBrlDecimal.Should().Be(-50m);
        h.SpotUtilizado.Should().Be(5.50m);
        h.TipoCotacao.Should().Be("PTAX_D1");
        h.RegistradoEm.Should().Be(novoInstante);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HistoricoMtmDiario CriarComPayoff(decimal payoff) =>
        HistoricoMtmDiario.Criar(HedgeIdFixo, DataFixa, payoff, 5.25m, "SPOT_INTRADAY", InstanteFixo);

    /// <summary>
    /// Reproduz a lógica de mapeamento de Posicao que vive nos handlers —
    /// testamos aqui o sinal do campo backing field, não o mapeamento em si.
    /// </summary>
    private static string DerivarPosicao(HistoricoMtmDiario h) =>
        h.PayoffBrlDecimal > 0 ? "RECEBER"
      : h.PayoffBrlDecimal < 0 ? "PAGAR"
      : "NEUTRO";
}

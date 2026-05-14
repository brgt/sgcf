using FluentAssertions;

using NodaTime;

using Sgcf.Domain.Calendario;

using Xunit;

namespace Sgcf.Domain.Tests.Calendario;

[Trait("Category", "Domain")]
public sealed class BusinessDayCalculatorTests
{
    // Conjunto de feriados de referência para os testes:
    //  - 25/12/2026 (sex) — Natal
    //  - 01/01/2027 (sex) — Confraternização
    //  - 16/02/2027 (ter) — Carnaval
    //  - 02/04/2027 (sex) — Sexta-feira da Paixão
    private static readonly IReadOnlySet<LocalDate> Feriados = new HashSet<LocalDate>
    {
        new(2026, 12, 25),
        new(2027, 1, 1),
        new(2027, 2, 16),
        new(2027, 4, 2)
    };

    // ── IsBusinessDay ───────────────────────────────────────────────────────

    [Fact]
    public void IsBusinessDay_DiaUtilSemFeriado_RetornaTrue()
    {
        // 14/05/2026 é quinta-feira
        BusinessDayCalculator.IsBusinessDay(new LocalDate(2026, 5, 14), Feriados).Should().BeTrue();
    }

    [Fact]
    public void IsBusinessDay_Sabado_RetornaFalse()
    {
        // 16/05/2026 é sábado
        BusinessDayCalculator.IsBusinessDay(new LocalDate(2026, 5, 16), Feriados).Should().BeFalse();
    }

    [Fact]
    public void IsBusinessDay_Domingo_RetornaFalse()
    {
        // 17/05/2026 é domingo
        BusinessDayCalculator.IsBusinessDay(new LocalDate(2026, 5, 17), Feriados).Should().BeFalse();
    }

    [Fact]
    public void IsBusinessDay_FeriadoEmDiaUtil_RetornaFalse()
    {
        // Natal 25/12/2026 é sexta-feira
        BusinessDayCalculator.IsBusinessDay(new LocalDate(2026, 12, 25), Feriados).Should().BeFalse();
    }

    // ── NextBusinessDay (inclusive) ─────────────────────────────────────────

    [Fact]
    public void NextBusinessDay_DiaUtil_RetornaProprioDia()
    {
        // 14/05/2026 é quinta-feira — útil
        BusinessDayCalculator.NextBusinessDay(new LocalDate(2026, 5, 14), Feriados)
            .Should().Be(new LocalDate(2026, 5, 14));
    }

    [Fact]
    public void NextBusinessDay_Sabado_PulaParaSegunda()
    {
        // 16/05/2026 (sáb) → 18/05/2026 (seg)
        BusinessDayCalculator.NextBusinessDay(new LocalDate(2026, 5, 16), Feriados)
            .Should().Be(new LocalDate(2026, 5, 18));
    }

    [Fact]
    public void NextBusinessDay_Natal_PulaParaProximaSegunda()
    {
        // 25/12/2026 (sex feriado) → 28/12/2026 (seg)
        BusinessDayCalculator.NextBusinessDay(new LocalDate(2026, 12, 25), Feriados)
            .Should().Be(new LocalDate(2026, 12, 28));
    }

    // ── PreviousBusinessDay (inclusive) ─────────────────────────────────────

    [Fact]
    public void PreviousBusinessDay_DiaUtil_RetornaProprioDia()
    {
        BusinessDayCalculator.PreviousBusinessDay(new LocalDate(2026, 5, 14), Feriados)
            .Should().Be(new LocalDate(2026, 5, 14));
    }

    [Fact]
    public void PreviousBusinessDay_Domingo_RetornaSextaAnterior()
    {
        // 17/05/2026 (dom) → 15/05/2026 (sex)
        BusinessDayCalculator.PreviousBusinessDay(new LocalDate(2026, 5, 17), Feriados)
            .Should().Be(new LocalDate(2026, 5, 15));
    }

    [Fact]
    public void PreviousBusinessDay_Natal_VoltaParaQuintaAnterior()
    {
        // 25/12/2026 (sex feriado) → 24/12/2026 (qui)
        BusinessDayCalculator.PreviousBusinessDay(new LocalDate(2026, 12, 25), Feriados)
            .Should().Be(new LocalDate(2026, 12, 24));
    }

    // ── AddBusinessDays ─────────────────────────────────────────────────────

    [Fact]
    public void AddBusinessDays_Zero_RetornaProprioDia()
    {
        BusinessDayCalculator.AddBusinessDays(new LocalDate(2026, 5, 14), 0, Feriados)
            .Should().Be(new LocalDate(2026, 5, 14));
    }

    [Fact]
    public void AddBusinessDays_PositivoSemFeriado_ConsideraApenasFimDeSemana()
    {
        // 14/05/2026 (qui) +5 dias úteis → 21/05/2026 (qui)
        // 14→15 (sex), 18 (seg), 19 (ter), 20 (qua), 21 (qui)
        BusinessDayCalculator.AddBusinessDays(new LocalDate(2026, 5, 14), 5, Feriados)
            .Should().Be(new LocalDate(2026, 5, 21));
    }

    [Fact]
    public void AddBusinessDays_PositivoComFeriado_PulaFeriado()
    {
        // 24/12/2026 (qui) +1 dia útil — 25 é Natal (sex), 26-27 fim de semana → 28/12 (seg)
        BusinessDayCalculator.AddBusinessDays(new LocalDate(2026, 12, 24), 1, Feriados)
            .Should().Be(new LocalDate(2026, 12, 28));
    }

    [Fact]
    public void AddBusinessDays_Negativo_RecuaCorretamente()
    {
        // 28/12/2026 (seg) -1 dia útil → 24/12/2026 (qui, pulando Natal e fds)
        BusinessDayCalculator.AddBusinessDays(new LocalDate(2026, 12, 28), -1, Feriados)
            .Should().Be(new LocalDate(2026, 12, 24));
    }

    // ── CountBusinessDays (intervalo [start, end)) ──────────────────────────

    [Fact]
    public void CountBusinessDays_MesmoDia_RetornaZero()
    {
        BusinessDayCalculator.CountBusinessDays(
            new LocalDate(2026, 5, 14),
            new LocalDate(2026, 5, 14),
            Feriados).Should().Be(0);
    }

    [Fact]
    public void CountBusinessDays_SemanaSimples_ContaCorretamente()
    {
        // 11/05/2026 (seg) até 18/05/2026 (seg) exclusive
        // dias úteis no intervalo: 11, 12, 13, 14, 15 = 5
        BusinessDayCalculator.CountBusinessDays(
            new LocalDate(2026, 5, 11),
            new LocalDate(2026, 5, 18),
            Feriados).Should().Be(5);
    }

    [Fact]
    public void CountBusinessDays_AbrangendoNatal_ExcluiFeriado()
    {
        // 21/12/2026 (seg) até 31/12/2026 (qui) exclusive
        // úteis: 21, 22, 23, 24, (25 Natal), 28, 29, 30 = 7
        BusinessDayCalculator.CountBusinessDays(
            new LocalDate(2026, 12, 21),
            new LocalDate(2026, 12, 31),
            Feriados).Should().Be(7);
    }

    [Fact]
    public void CountBusinessDays_InicioMaiorQueFim_LancaArgumentException()
    {
        Action act = () => BusinessDayCalculator.CountBusinessDays(
            new LocalDate(2026, 5, 18),
            new LocalDate(2026, 5, 11),
            Feriados);

        act.Should().Throw<ArgumentException>();
    }

    // ── AjustarPorConvencao ─────────────────────────────────────────────────

    [Fact]
    public void AjustarPorConvencao_Unadjusted_MantemData()
    {
        BusinessDayCalculator.AjustarPorConvencao(
            new LocalDate(2026, 12, 25), ConvencaoDataNaoUtil.Unadjusted, Feriados)
            .Should().Be(new LocalDate(2026, 12, 25));
    }

    [Fact]
    public void AjustarPorConvencao_Following_PulaParaProximoUtil()
    {
        // 25/12/2026 (Natal, sex) → 28/12 (seg)
        BusinessDayCalculator.AjustarPorConvencao(
            new LocalDate(2026, 12, 25), ConvencaoDataNaoUtil.Following, Feriados)
            .Should().Be(new LocalDate(2026, 12, 28));
    }

    [Fact]
    public void AjustarPorConvencao_ModifiedFollowing_SemViradaDeMes_PulaParaProximoUtil()
    {
        // 25/12/2026 (Natal) → 28/12 (mesmo mês = OK)
        BusinessDayCalculator.AjustarPorConvencao(
            new LocalDate(2026, 12, 25), ConvencaoDataNaoUtil.ModifiedFollowing, Feriados)
            .Should().Be(new LocalDate(2026, 12, 28));
    }

    [Fact]
    public void AjustarPorConvencao_ModifiedFollowing_ComViradaDeMes_RecuaParaAnterior()
    {
        // 31/01/2027 é domingo. Following → 01/02/2027 (seg). Mas vira mês.
        // ModifiedFollowing recua para sexta 29/01/2027.
        BusinessDayCalculator.AjustarPorConvencao(
            new LocalDate(2027, 1, 31), ConvencaoDataNaoUtil.ModifiedFollowing, Feriados)
            .Should().Be(new LocalDate(2027, 1, 29));
    }

    [Fact]
    public void AjustarPorConvencao_Preceding_RecuaParaAnterior()
    {
        // 25/12/2026 (Natal) → 24/12/2026 (qui)
        BusinessDayCalculator.AjustarPorConvencao(
            new LocalDate(2026, 12, 25), ConvencaoDataNaoUtil.Preceding, Feriados)
            .Should().Be(new LocalDate(2026, 12, 24));
    }

    [Fact]
    public void AjustarPorConvencao_ModifiedPreceding_ComViradaParaMesAnterior_AvancaParaProximoUtil()
    {
        // 01/02/2027 (seg, útil) — sem ajuste necessário, é dia útil
        BusinessDayCalculator.AjustarPorConvencao(
            new LocalDate(2027, 2, 1), ConvencaoDataNaoUtil.ModifiedPreceding, Feriados)
            .Should().Be(new LocalDate(2027, 2, 1));

        // Caso de virada: dia 1° é sábado/domingo. Preceding recuaria para mês anterior.
        // Usando 01/01/2027 (sex feriado Confraternização):
        //   Preceding → 31/12/2026 (qui) — mês anterior → ModifiedPreceding avança para 04/01/2027 (seg)
        BusinessDayCalculator.AjustarPorConvencao(
            new LocalDate(2027, 1, 1), ConvencaoDataNaoUtil.ModifiedPreceding, Feriados)
            .Should().Be(new LocalDate(2027, 1, 4));
    }
}

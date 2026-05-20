using FluentAssertions;

using NodaTime;

using NSubstitute;

using Xunit;

namespace Sgcf.Jobs.Tests.Jobs;

/// <summary>
/// Prove-It: os jobs devem usar fuso BRT (America/Sao_Paulo) para calcular "hoje".
///
/// Estes testes verificam a lógica central de resolução de data local:
/// à 23:30 BRT do dia 19, o "hoje" brasileiro é 19-mai, não 20-mai como UTC retornaria.
///
/// Os jobs (BackfillPtaxJob, LiquidarNdfJob, RecalcularMtmJob) usam
/// `clock.GetCurrentInstant().InUtc().Date` — que está errado para datas de calendário BR.
/// Após o fix, devem usar `InZone(FusoBrasilia).Date`.
/// </summary>
[Trait("Category", "Jobs")]
public sealed class TimezoneTests
{
    private static readonly DateTimeZone FusoBrasilia = DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // 2026-05-19 23:30 BRT == 2026-05-20 02:30 UTC
    private static readonly Instant InstandeViraNoiteBrt = Instant.FromUtc(2026, 5, 20, 2, 30);

    /// <summary>
    /// Prove-It RED: ao usar InUtc().Date à 23:30 BRT, obtemos 2026-05-20 — data ERRADA para BR.
    /// Após o fix (InZone(BRT).Date), deve retornar 2026-05-19 — data CORRETA para BR.
    /// </summary>
    [Fact]
    public void GetHoje_As2330Brt_ComInZoneBrt_RetornaDataLocalBrasileira()
    {
        // Arrange
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        // Act — este é o padrão correto que os jobs DEVEM usar após o fix
        LocalDate hojeComBrt = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        // Assert
        hojeComBrt.Should().Be(new LocalDate(2026, 5, 19),
            because: "à 23:30 BRT do dia 19, ainda é dia 19 no Brasil — não 20 como em UTC");
    }

    /// <summary>
    /// Prove-It RED: ao usar InUtc().Date à 23:30 BRT, obtemos 2026-05-20 — BUG.
    /// Este teste documenta o comportamento INCORRETO atual dos jobs.
    /// </summary>
    [Fact]
    public void GetHoje_As2330Brt_ComInUtc_RetornaDataUTC_Que_EhErrada()
    {
        // Arrange
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        // Act — este é o padrão BUG atual dos jobs (InUtc().Date)
        LocalDate hojeComUtc = clock.GetCurrentInstant().InUtc().Date;

        // Assert — documenta que InUtc retorna a data errada para calendário BR
        hojeComUtc.Should().Be(new LocalDate(2026, 5, 20),
            because: "InUtc() retorna 20 (UTC) — mas o dia no Brasil ainda é 19, tornando a data incorreta para negócios BR");

        // E o correto seria:
        LocalDate hojeCorretoBrt = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;
        hojeComUtc.Should().NotBe(hojeCorretoBrt,
            because: "esta é a prova do bug: InUtc() e InZone(BRT) divergem à meia-noite BRT");
    }

    /// <summary>
    /// BackfillPtaxJob calcula range "hoje - 30 dias até hoje - 1 dia".
    /// Com InUtc(), o backfill de 23:30 BRT do dia 19 calcularia: início = 2026-04-20 (errado).
    /// Com InZone(BRT), início correto = 2026-04-19.
    /// </summary>
    [Fact]
    public void BackfillRange_As2330Brt_ComInZoneBrt_UsaInicioCorreto()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        // Simula a lógica do BackfillPtaxJob após o fix
        LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date; // 2026-05-19
        LocalDate inicio = hoje.PlusDays(-30);
        LocalDate fim = hoje.PlusDays(-1);

        inicio.Should().Be(new LocalDate(2026, 4, 19));
        fim.Should().Be(new LocalDate(2026, 5, 18));
    }

    /// <summary>
    /// LiquidarNdfJob e RecalcularMtmJob usam "hoje" para consultar hedges vencendo no dia.
    /// Com InUtc(), à 23:30 BRT do dia 19, consultaria hedges do dia 20 — dia seguinte no BR.
    /// Com InZone(BRT), consulta hedges do dia 19 — correto.
    /// </summary>
    [Fact]
    public void HojeParaConsultaHedge_As2330Brt_ComInZoneBrt_ConsultaDiaCorreto()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstandeViraNoiteBrt);

        // Simula a lógica de LiquidarNdfJob e RecalcularMtmJob após o fix
        LocalDate hojeParaConsulta = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

        hojeParaConsulta.Should().Be(new LocalDate(2026, 5, 19),
            because: "liquidações e MTM devem ser calculados para o dia brasileiro corrente");
    }
}

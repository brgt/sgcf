using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

using Xunit;

namespace Sgcf.Domain.Tests.Simulacao;

/// <summary>
/// Prove-It tests para o bug de timezone em SimulacaoContratacao.Criar (invariante I-2).
///
/// Contexto do bug: o invariante I-2 derivava "hoje" via <c>agora.InUtc().Date</c>.
/// Entre 21h e 23:59:59 BRT (00h–02:59 UTC do dia seguinte), a data UTC já avançou
/// para o próximo dia, mas o calendário brasileiro ainda está no dia anterior.
/// Um POST com <c>dataContratacaoPrevista = hoje BRT</c> era rejeitado indevidamente.
///
/// Correção esperada: usar <c>InZone(FusoBrasilia).Date</c> para derivar datas de calendário.
/// </summary>
[Trait("Category", "Domain")]
public sealed class SimulacaoContratacaoTimezoneTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Clock fixado em 2026-05-20 02:30 UTC = 2026-05-19 23:30 BRT.
    /// Em UTC "hoje" é 20/mai; em BRT "hoje" ainda é 19/mai.
    /// </summary>
    private static IClock ClockMeiaNoiteUtc()
    {
        // 2026-05-20T02:30:00Z  =>  2026-05-19T23:30:00-03:00 (BRT)
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 20, 2, 30));
        return clock;
    }

    private static SimulacaoContratacao CriarSimulacaoMinima(
        LocalDate dataContratacaoPrevista,
        IClock clock)
    {
        return SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.CapitalDeGiro,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(1_000_000m, Moeda.Brl),
            dataContratacaoPrevista: dataContratacaoPrevista,
            dataPrimeiroVencimento: dataContratacaoPrevista.PlusDays(30),
            tipoTaxa: TipoTaxa.CdiSpread,
            taxaAa: null,
            spreadAa: Percentual.De(3m),
            baseCalculo: BaseCalculo.Dias252,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Bullet,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: clock,
            anoBase: 2026);
    }

    // ── Prove-It: RED antes do fix, GREEN após ────────────────────────────────

    /// <summary>
    /// 23:30 BRT = 02:30 UTC do dia seguinte.
    /// dataContratacaoPrevista = 2026-05-19 = "hoje BRT".
    /// Com InUtc().Date, "hoje UTC" já seria 2026-05-20 → rejeita indevidamente.
    /// Com InZone(BRT).Date, "hoje BRT" = 2026-05-19 → aceita corretamente.
    /// </summary>
    [Fact]
    public void Criar_DataContratacaoIgualHojeBrt_AceitaQuandoUtcJaPassouMeiaNoite()
    {
        // Arrange: clock em 23:30 BRT (02:30 UTC do dia 20) — UTC já é "amanhã"
        IClock clock = ClockMeiaNoiteUtc();

        // "Hoje" no calendário brasileiro ainda é 19-mai-2026
        var dataHojeBrt = new LocalDate(2026, 5, 19);

        // Act
        Action ato = () => CriarSimulacaoMinima(dataHojeBrt, clock);

        // Assert: não deve lançar — 2026-05-19 é "hoje" em BRT
        ato.Should().NotThrow(
            "23:30 BRT corresponde a 02:30 UTC do dia seguinte; " +
            "o invariante I-2 deve usar fuso BRT, não UTC");
    }

    /// <summary>
    /// Confirma que datas genuinamente no passado (BRT) continuam sendo rejeitadas.
    /// Isso garante que o fix não enfraquece o invariante — apenas o corrige.
    /// </summary>
    [Fact]
    public void Criar_DataContratacaoOntemBrt_Rejeita()
    {
        // Arrange: clock em 23:30 BRT (02:30 UTC do dia 20)
        IClock clock = ClockMeiaNoiteUtc();

        // "Ontem" em BRT: 2026-05-18 (dois dias antes de 2026-05-20 UTC = um dia antes de 2026-05-19 BRT)
        var dataOntemBrt = new LocalDate(2026, 5, 18);

        // Act
        Action ato = () => CriarSimulacaoMinima(dataOntemBrt, clock);

        // Assert: deve rejeitar — 2026-05-18 é passado mesmo em BRT
        ato.Should().Throw<ArgumentException>()
            .WithMessage("*contratacao*",
                because: "I-2 deve rejeitar datas passadas mesmo após o fix de timezone");
    }

    /// <summary>
    /// Confirma que datas UTC do dia seguinte (mas ainda "hoje BRT") são aceitas.
    /// Cenário mais extremo: 00:01 BRT = 03:01 UTC — um dia inteiro de diferença.
    /// </summary>
    [Fact]
    public void Criar_DataContratacaoHojeBrt_ComClockUm_MinutoAposMeiaNoiteBrt_Aceita()
    {
        // Arrange: 2026-05-20 03:01 UTC = 2026-05-20 00:01 BRT
        // "Hoje BRT" = 2026-05-20; "Hoje UTC" = 2026-05-20 (coincide neste caso)
        // Vamos testar o caso contrário: 2026-05-19 23:01 BRT = 2026-05-20 02:01 UTC
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 20, 2, 1));

        // "Hoje BRT" ainda é 2026-05-19 (são 23:01 BRT)
        var dataHojeBrt = new LocalDate(2026, 5, 19);

        Action ato = () => CriarSimulacaoMinima(dataHojeBrt, clock);

        ato.Should().NotThrow(
            "23:01 BRT = 02:01 UTC do próximo dia; hoje BRT ainda é 19-mai");
    }
}

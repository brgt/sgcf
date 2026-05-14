using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Calendario;
using Sgcf.Domain.Calendario;
using Sgcf.Infrastructure.Calendario;

using Xunit;

namespace Sgcf.Application.Tests.Calendario;

[Trait("Category", "Domain")]
public sealed class BusinessDayCalendarTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    private static Feriado CriarFeriado(LocalDate data, string descricao, IClock clock) =>
        Feriado.Criar(
            data, TipoFeriado.Nacional, EscopoFeriado.Brasil,
            descricao, FonteFeriado.Anbima, data.Year, clock);

    private static (IBusinessDayCalendar calendar, IFeriadoRepository repo) BuildCalendarComFeriados(
        params (LocalDate data, string descricao)[] feriados)
    {
        IClock clock = CriarClock(Instant.FromUtc(2026, 1, 1, 0, 0));
        IFeriadoRepository repo = Substitute.For<IFeriadoRepository>();

        var porAno = feriados
            .GroupBy(f => f.data.Year)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => CriarFeriado(f.data, f.descricao, clock)).ToList().AsReadOnly());

        repo.ListByYearAsync(Arg.Any<int>(), Arg.Any<EscopoFeriado?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                int ano = call.Arg<int>();
                if (porAno.TryGetValue(ano, out var lista))
                {
                    return Task.FromResult<IReadOnlyList<Feriado>>(lista);
                }
                return Task.FromResult<IReadOnlyList<Feriado>>(Array.Empty<Feriado>());
            });

        return (new BusinessDayCalendar(repo), repo);
    }

    // ── IsBusinessDayAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task IsBusinessDayAsync_DiaUtilSemFeriado_RetornaTrue()
    {
        (IBusinessDayCalendar c, _) = BuildCalendarComFeriados();
        (await c.IsBusinessDayAsync(new LocalDate(2026, 5, 14))).Should().BeTrue();
    }

    [Fact]
    public async Task IsBusinessDayAsync_Sabado_RetornaFalse()
    {
        (IBusinessDayCalendar c, _) = BuildCalendarComFeriados();
        (await c.IsBusinessDayAsync(new LocalDate(2026, 5, 16))).Should().BeFalse();
    }

    [Fact]
    public async Task IsBusinessDayAsync_Feriado_RetornaFalse()
    {
        (IBusinessDayCalendar c, _) = BuildCalendarComFeriados(
            (new LocalDate(2026, 12, 25), "Natal"));
        (await c.IsBusinessDayAsync(new LocalDate(2026, 12, 25))).Should().BeFalse();
    }

    // ── AjustarPorConvencaoAsync ────────────────────────────────────────────

    [Fact]
    public async Task AjustarPorConvencaoAsync_Following_PulaFeriado()
    {
        (IBusinessDayCalendar c, _) = BuildCalendarComFeriados(
            (new LocalDate(2026, 12, 25), "Natal"));
        (await c.AjustarPorConvencaoAsync(
            new LocalDate(2026, 12, 25), ConvencaoDataNaoUtil.Following))
            .Should().Be(new LocalDate(2026, 12, 28));
    }

    [Fact]
    public async Task AjustarPorConvencaoAsync_Unadjusted_MantemData()
    {
        (IBusinessDayCalendar c, _) = BuildCalendarComFeriados(
            (new LocalDate(2026, 12, 25), "Natal"));
        (await c.AjustarPorConvencaoAsync(
            new LocalDate(2026, 12, 25), ConvencaoDataNaoUtil.Unadjusted))
            .Should().Be(new LocalDate(2026, 12, 25));
    }

    // ── Cache: chamadas repetidas no mesmo ano consultam o repo apenas uma vez ─

    [Fact]
    public async Task IsBusinessDayAsync_ChamadasMultiplas_RepositorioConsultadoUmaUnicaVez()
    {
        (IBusinessDayCalendar c, IFeriadoRepository repo) = BuildCalendarComFeriados(
            (new LocalDate(2026, 12, 25), "Natal"));

        await c.IsBusinessDayAsync(new LocalDate(2026, 5, 14));
        await c.IsBusinessDayAsync(new LocalDate(2026, 6, 10));
        await c.IsBusinessDayAsync(new LocalDate(2026, 12, 25));

        await repo.Received(1).ListByYearAsync(
            2026, EscopoFeriado.Brasil, Arg.Any<CancellationToken>());
    }

    // ── AddBusinessDaysAsync atravessando fim de ano ────────────────────────

    [Fact]
    public async Task AddBusinessDaysAsync_AtravessandoVirada_CarregaAnosAdjacentes()
    {
        (IBusinessDayCalendar c, IFeriadoRepository repo) = BuildCalendarComFeriados(
            (new LocalDate(2026, 12, 25), "Natal"),
            (new LocalDate(2027, 1, 1), "Confraternização"));

        // 23/12/2026 (qua) + 5 dias úteis:
        // 24 (qui), [25 Natal pulado], 28 (seg), 29 (ter), 30 (qua), 31 (qui)
        LocalDate result = await c.AddBusinessDaysAsync(new LocalDate(2026, 12, 23), 5);
        result.Should().Be(new LocalDate(2026, 12, 31));

        // Garante que carregou 2025, 2026 e 2027 (vizinhança ano-1, ano, ano+1)
        await repo.Received(1).ListByYearAsync(2025, EscopoFeriado.Brasil, Arg.Any<CancellationToken>());
        await repo.Received(1).ListByYearAsync(2026, EscopoFeriado.Brasil, Arg.Any<CancellationToken>());
        await repo.Received(1).ListByYearAsync(2027, EscopoFeriado.Brasil, Arg.Any<CancellationToken>());
    }

    // ── CountBusinessDaysAsync atravessando anos ────────────────────────────

    [Fact]
    public async Task CountBusinessDaysAsync_RangeCruzandoAno_UsaFeriadosDosDoisAnos()
    {
        (IBusinessDayCalendar c, _) = BuildCalendarComFeriados(
            (new LocalDate(2026, 12, 25), "Natal"),
            (new LocalDate(2027, 1, 1), "Confraternização"));

        // [21/12/2026, 04/01/2027): dias úteis
        // dez 2026: 21,22,23,24,28,29,30,31 = 8 (25 e fds excluídos)
        // jan 2027: nenhum (01 feriado, 02-03 fds)
        // total = 8
        int count = await c.CountBusinessDaysAsync(
            new LocalDate(2026, 12, 21), new LocalDate(2027, 1, 4));

        count.Should().Be(8);
    }
}

using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Application.Tesouraria;
using Sgcf.Application.Tesouraria.Queries;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;
using Xunit;

namespace Sgcf.Application.Tests.Tesouraria;

[Trait("Category", "Application")]
public sealed class FluxoCaixaQueryTests
{
    private static IClock CriarClock(Instant instant)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instant);
        return clock;
    }

    // Instant correspondente a 2026-05-21 12:00 UTC → data BRT = 2026-05-21
    private static readonly Instant InstanteBase = Instant.FromUtc(2026, 5, 21, 15, 0);

    private static GetFluxoCaixaQueryHandler CriarHandler(
        IClock clock,
        IEventoCronogramaRepository? cronogramaRepo = null,
        IEventoFluxoCaixaRepository? fluxoRepo = null,
        ICotacaoSpotCache? spotCache = null,
        ICotacaoFxRepository? cotacaoFxRepo = null)
    {
        // Cria substitutes com retornos vazios somente quando não fornecidos pelo chamador.
        if (cronogramaRepo is null)
        {
            cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
            cronogramaRepo.ListPrevistosNoPeriodoAsync(
                Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
                .Returns(new List<Domain.Cronograma.EventoCronograma>().AsReadOnly()
                    as IReadOnlyList<Domain.Cronograma.EventoCronograma>);
        }

        if (fluxoRepo is null)
        {
            fluxoRepo = Substitute.For<IEventoFluxoCaixaRepository>();
            fluxoRepo.ListByPeriodoAsync(
                Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
                .Returns(new List<EventoFluxoCaixa>().AsReadOnly()
                    as IReadOnlyList<EventoFluxoCaixa>);
        }

        spotCache ??= Substitute.For<ICotacaoSpotCache>();
        cotacaoFxRepo ??= Substitute.For<ICotacaoFxRepository>();

        return new GetFluxoCaixaQueryHandler(cronogramaRepo, fluxoRepo, spotCache, cotacaoFxRepo, clock);
    }

    [Fact]
    public async Task Handle_SemEventos_RetornaDiasComSaldoZero()
    {
        // Arrange — repositórios vazios, janela de 3 dias.
        IClock clock = CriarClock(InstanteBase);
        GetFluxoCaixaQueryHandler handler = CriarHandler(clock);

        var query = new GetFluxoCaixaQuery("2026-05-21", "2026-05-23");

        // Act
        var resultado = await handler.Handle(query, CancellationToken.None);

        // Assert
        resultado.Data.Should().HaveCount(3);
        resultado.Data.Should().AllSatisfy(d =>
        {
            d.EntradasBrl.Should().Be(0m);
            d.SaidasBrl.Should().Be(0m);
            d.SaldoProjetadoBrl.Should().Be(0m);
            d.Eventos.Should().BeEmpty();
            d.Alertas.Should().BeEmpty();
        });

        resultado.Meta.Completude.Should().Be(Common.Completude.Completo);
    }

    [Fact]
    public async Task Handle_ComEventoSaidaNoDia_RetornaAlertaSeNegativo()
    {
        // Arrange — um único evento manual de saída sem nenhuma entrada → saldo projetado negativo.
        IClock clock = CriarClock(InstanteBase);

        LocalDate diaSaida = new(2026, 5, 21);
        Money valor = new(500m, Moeda.Brl);

        // Cria o evento manualmente com factory do domínio.
        EventoFluxoCaixa saida = EventoFluxoCaixa.Criar(
            diaSaida,
            TipoEventoFluxo.Saida,
            valor,
            "Pagamento fornecedor",
            "operador",
            clock);

        IReadOnlyList<EventoFluxoCaixa> eventosFluxo = [saida];

        IEventoFluxoCaixaRepository fluxoRepo = Substitute.For<IEventoFluxoCaixaRepository>();
        fluxoRepo.ListByPeriodoAsync(
            Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(eventosFluxo);

        GetFluxoCaixaQueryHandler handler = CriarHandler(clock, fluxoRepo: fluxoRepo);

        var query = new GetFluxoCaixaQuery("2026-05-21", "2026-05-21");

        // Act
        var resultado = await handler.Handle(query, CancellationToken.None);

        // Assert — dia único com saldo negativo deve gerar alerta.
        resultado.Data.Should().HaveCount(1);

        FluxoCaixaDiaDto dia = resultado.Data[0];
        dia.SaidasBrl.Should().Be(500m);
        dia.EntradasBrl.Should().Be(0m);
        dia.SaldoProjetadoBrl.Should().Be(-500m);
        dia.Alertas.Should().ContainSingle()
            .Which.Should().Contain("negativo");
    }
}

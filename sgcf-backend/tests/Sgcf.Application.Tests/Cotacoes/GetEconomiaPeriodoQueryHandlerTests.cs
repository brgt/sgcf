using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Queries;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

public sealed class GetEconomiaPeriodoQueryHandlerTests
{
    private static readonly Guid BancoA = Guid.Parse("019e21cb-bbe9-7b2b-acbc-2964be3bae43");
    private static readonly Guid BancoB = Guid.Parse("019e22d1-c90e-778f-81ca-a9508fe95802");
    private static readonly IClock Clock = TestHelpers.CriarClock();

    private static EconomiaNegociacao CriarEconomia(Guid bancoId, decimal economiaBrl, LocalDate dataRefCdi)
    {
        // Snapshot com BancoId mínimo para o agregador conseguir extrair
        string snapshotProposta = $"{{\"Id\":\"{Guid.NewGuid()}\",\"BancoId\":\"{bancoId}\",\"TaxaAaPercentual\":6.2}}";
        string snapshotContrato = "{\"Id\":\"" + Guid.NewGuid() + "\",\"TaxaAa\":6.0}";

        return EconomiaNegociacao.Criar(
            cotacaoId: Guid.NewGuid(),
            contratoId: Guid.NewGuid(),
            snapshotPropostaJson: snapshotProposta,
            snapshotContratoJson: snapshotContrato,
            cetPropostaAaPercentual: 7.10m,
            cetContratoAaPercentual: 6.92m,
            economiaBrl: new Money(economiaBrl, Moeda.Brl),
            economiaAjustadaCdiBrl: new Money(economiaBrl * 1.05m, Moeda.Brl),
            dataReferenciaCdi: dataRefCdi,
            clock: Clock);
    }

    [Fact]
    public async Task Handle_sem_economias_retorna_DTO_vazio()
    {
        IEconomiaRepository repo = Substitute.For<IEconomiaRepository>();
        repo.ListByPeriodoAsync(Arg.Any<YearMonth>(), Arg.Any<YearMonth>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EconomiaNegociacao>());

        var handler = new GetEconomiaPeriodoQueryHandler(repo);
        var query = new GetEconomiaPeriodoQuery(new YearMonth(2026, 5), new YearMonth(2026, 5), null);

        EconomiaPeriodoDto result = await handler.Handle(query, CancellationToken.None);

        result.TotalOperacoes.Should().Be(0);
        result.PorMes.Should().BeEmpty();
        result.PorBanco.Should().BeEmpty();
    }

    // Prove-It bug #5 — porBanco vazio mesmo com dados convertidos.
    [Fact]
    public async Task Handle_com_3_economias_2_bancos_agrega_porBanco_corretamente()
    {
        var economias = new[]
        {
            CriarEconomia(BancoA, economiaBrl: 12_000m, dataRefCdi: new LocalDate(2026, 5, 16)),
            CriarEconomia(BancoA, economiaBrl: 8_500m,  dataRefCdi: new LocalDate(2026, 5, 20)),
            CriarEconomia(BancoB, economiaBrl: 15_300m, dataRefCdi: new LocalDate(2026, 5, 25)),
        };

        IEconomiaRepository repo = Substitute.For<IEconomiaRepository>();
        repo.ListByPeriodoAsync(Arg.Any<YearMonth>(), Arg.Any<YearMonth>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(economias);

        var handler = new GetEconomiaPeriodoQueryHandler(repo);
        var query = new GetEconomiaPeriodoQuery(new YearMonth(2026, 5), new YearMonth(2026, 5), null);

        EconomiaPeriodoDto result = await handler.Handle(query, CancellationToken.None);

        result.PorBanco.Should().HaveCount(2, "deve agregar por banco quando há economias de bancos distintos");
        result.PorBanco.Should().Contain(b => b.BancoId == BancoA && b.QuantidadeOperacoes == 2);
        result.PorBanco.Should().Contain(b => b.BancoId == BancoB && b.QuantidadeOperacoes == 1);

        var bancoA = result.PorBanco.Single(b => b.BancoId == BancoA);
        bancoA.EconomiaBrutaBrl.Should().Be(20_500m, "soma BancoA = 12.000 + 8.500");

        var bancoB = result.PorBanco.Single(b => b.BancoId == BancoB);
        bancoB.EconomiaBrutaBrl.Should().Be(15_300m);
    }

    [Fact]
    public async Task Handle_com_economias_em_mes_unico_agrega_porMes()
    {
        var economias = new[]
        {
            CriarEconomia(BancoA, economiaBrl: 5_000m, dataRefCdi: new LocalDate(2026, 5, 10)),
            CriarEconomia(BancoB, economiaBrl: 3_000m, dataRefCdi: new LocalDate(2026, 5, 20)),
        };

        IEconomiaRepository repo = Substitute.For<IEconomiaRepository>();
        repo.ListByPeriodoAsync(Arg.Any<YearMonth>(), Arg.Any<YearMonth>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(economias);

        var handler = new GetEconomiaPeriodoQueryHandler(repo);
        var query = new GetEconomiaPeriodoQuery(new YearMonth(2026, 5), new YearMonth(2026, 5), null);

        EconomiaPeriodoDto result = await handler.Handle(query, CancellationToken.None);

        result.PorMes.Should().HaveCount(1);
        result.PorMes[0].EconomiaBrutaBrl.Should().Be(8_000m);
        result.TotalEconomiaBrutaBrl.Should().Be(8_000m);
        result.TotalOperacoes.Should().Be(2);
    }
}

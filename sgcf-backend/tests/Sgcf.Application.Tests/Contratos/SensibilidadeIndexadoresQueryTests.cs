using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Queries;
using Sgcf.Application.Common;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

using Xunit;

namespace Sgcf.Application.Tests.Contratos;

/// <summary>
/// Testes unitários do handler <see cref="GetSensibilidadeIndexadoresQueryHandler"/> usando mocks (NSubstitute).
/// Verifica a lógica de agregação, conversão FX e classificação por indexador
/// sem tocar banco de dados.
/// </summary>
[Trait("Category", "Domain")]
public sealed class SensibilidadeIndexadoresQueryTests
{
    private static readonly Instant AgoraFixo = Instant.FromUtc(2026, 5, 21, 12, 0, 0);

    // Datas consistentes com AgoraFixo no fuso BRT (UTC-3) → 2026-05-21
    private static readonly LocalDate DataHoje = new(2026, 5, 21);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgoraFixo);
        return clock;
    }

    private static GetSensibilidadeIndexadoresQueryHandler CriarHandler(
        IContratoRepository contratoRepo,
        IEventoCronogramaRepository eventoRepo,
        ICotacaoSpotCache spotCache,
        IResolveTipoCotacaoService cotacaoResolver)
    {
        return new GetSensibilidadeIndexadoresQueryHandler(
            contratoRepo,
            eventoRepo,
            spotCache,
            cotacaoResolver,
            CriarClock());
    }

    // ---------------------------------------------------------------
    // Cenário 1: portfólio vazio
    // ---------------------------------------------------------------

    [Fact]
    public async Task Handle_PortfolioVazio_RetornaZeros()
    {
        // Arrange
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo
            .ListAtivosValoresPrincipaisAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<(Guid, decimal, Moeda)>());

        IEventoCronogramaRepository eventoRepo = Substitute.For<IEventoCronogramaRepository>();
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        IResolveTipoCotacaoService cotacaoResolver = Substitute.For<IResolveTipoCotacaoService>();

        GetSensibilidadeIndexadoresQueryHandler handler = CriarHandler(
            contratoRepo, eventoRepo, spotCache, cotacaoResolver);

        // Act
        EnvelopeResponse<SensibilidadeIndexadoresDto> resposta =
            await handler.Handle(new GetSensibilidadeIndexadoresQuery(DeltaBps: 100), CancellationToken.None);

        // Assert
        resposta.Data.SaldoDevedorTotalBrl.Should().Be(0m);
        resposta.Data.DeltaCustoAnualTotalBrl.Should().Be(0m);
        resposta.Data.PorIndexador.Should().BeEmpty();
        resposta.Data.DeltaBps.Should().Be(100);
        resposta.Meta.Completude.Should().Be(Completude.Completo);

        // Repositório de eventos não deve ser consultado quando não há contratos
        await eventoRepo.DidNotReceive().ListPrevistosNoPeriodoAsync(
            Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Cenário 2: 1 contrato BRL (CDI) + 1 contrato USD (SOFR)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Handle_ContratosBrlEFx_AgregaPorIndexador()
    {
        // Arrange
        Guid idBrl = Guid.NewGuid();
        Guid idUsd = Guid.NewGuid();

        // Contratos ativos: BRL 1M e USD 500k
        (Guid, decimal, Moeda)[] contratosAtivos =
        [
            (idBrl, 1_000_000m, Moeda.Brl),
            (idUsd, 500_000m, Moeda.Usd),
        ];

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo
            .ListAtivosValoresPrincipaisAsync(Arg.Any<CancellationToken>())
            .Returns(contratosAtivos);

        // Eventos futuros: cada contrato tem um único evento Principal com seu valor total
        EventoCronograma eventoBrl = EventoCronograma.Criar(
            contratoId: idBrl,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: DataHoje.PlusMonths(6),
            valorMoedaOriginal: new Money(1_000_000m, Moeda.Brl));

        EventoCronograma eventoUsd = EventoCronograma.Criar(
            contratoId: idUsd,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: DataHoje.PlusMonths(6),
            valorMoedaOriginal: new Money(500_000m, Moeda.Usd));

        IEventoCronogramaRepository eventoRepo = Substitute.For<IEventoCronogramaRepository>();
        eventoRepo
            .ListPrevistosNoPeriodoAsync(
                Arg.Any<LocalDate>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(new[] { eventoBrl, eventoUsd });

        // USD/BRL spot = 1.00 (simplifica a conversão: 500k USD = 500k BRL)
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache
            .GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>())
            .Returns(new Money(1m, Moeda.Brl));

        IResolveTipoCotacaoService cotacaoResolver = Substitute.For<IResolveTipoCotacaoService>();

        GetSensibilidadeIndexadoresQueryHandler handler = CriarHandler(
            contratoRepo, eventoRepo, spotCache, cotacaoResolver);

        // Act
        EnvelopeResponse<SensibilidadeIndexadoresDto> resposta =
            await handler.Handle(new GetSensibilidadeIndexadoresQuery(DeltaBps: 100), CancellationToken.None);

        SensibilidadeIndexadoresDto dados = resposta.Data;

        // Assert — totais
        // 1_000_000 (BRL) + 500_000 (USD × 1.00) = 1_500_000
        dados.SaldoDevedorTotalBrl.Should().Be(1_500_000m);

        // delta = 1_500_000 × 100 / 10_000 = 15_000
        dados.DeltaCustoAnualTotalBrl.Should().Be(15_000m);
        dados.DeltaBps.Should().Be(100);

        // Assert — por indexador
        dados.PorIndexador.Should().HaveCount(2);

        SensibilidadePorIndexadorDto grupoCdi = dados.PorIndexador
            .Single(g => g.Indexador == "CDI");
        grupoCdi.QuantidadeContratos.Should().Be(1);
        grupoCdi.SaldoDevedorBrl.Should().Be(1_000_000m);
        // 1_000_000 × 100 / 10_000 = 10_000
        grupoCdi.DeltaCustoAnualBrl.Should().Be(10_000m);

        SensibilidadePorIndexadorDto grupoSofr = dados.PorIndexador
            .Single(g => g.Indexador == "SOFR");
        grupoSofr.QuantidadeContratos.Should().Be(1);
        grupoSofr.SaldoDevedorBrl.Should().Be(500_000m);
        // 500_000 × 100 / 10_000 = 5_000
        grupoSofr.DeltaCustoAnualBrl.Should().Be(5_000m);

        // Completude deve ser Completo pois spot estava disponível
        resposta.Meta.Completude.Should().Be(Completude.Completo);
    }
}

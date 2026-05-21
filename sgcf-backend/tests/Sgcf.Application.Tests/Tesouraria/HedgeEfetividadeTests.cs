using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Common;
using Sgcf.Application.Contratos;
using Sgcf.Application.Hedge;
using Sgcf.Application.Tesouraria;
using Sgcf.Application.Tesouraria.Queries;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Hedge;
using Xunit;

namespace Sgcf.Application.Tests.Tesouraria;

/// <summary>
/// Testes unitários para <see cref="GetHedgeEfetividadeQueryHandler"/>.
/// Todas as dependências são substituídas por NSubstitute — sem Testcontainers.
/// </summary>
public sealed class HedgeEfetividadeTests
{
    // ── Constantes de cenário ──────────────────────────────────────────────────

    private static readonly Instant InstanteFixo =
        Instant.FromUtc(2026, 1, 15, 12, 0, 0);

    // Taxa spot USD/BRL usada nos cenários
    private const decimal SpotUsd = 5.10m;

    // ── Fábrica de dependências ────────────────────────────────────────────────

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    /// <summary>
    /// Configura o spot cache com a taxa informada para USD.
    /// Retorna null para todas as outras moedas.
    /// </summary>
    private static ICotacaoSpotCache CriarSpotCacheUsd(decimal taxaUsd = SpotUsd)
    {
        ICotacaoSpotCache cache = Substitute.For<ICotacaoSpotCache>();
        cache.GetSpotAsync(Moeda.Usd, Arg.Any<CancellationToken>())
             .Returns(new Money(taxaUsd, Moeda.Brl));
        cache.GetSpotAsync(Arg.Is<Moeda>(m => m != Moeda.Usd), Arg.Any<CancellationToken>())
             .Returns((Money?)null);
        return cache;
    }

    /// <summary>
    /// Configura o repositório FX para retornar null — forçando o fluxo spot-only.
    /// </summary>
    private static ICotacaoFxRepository CriarFxRepoVazio()
    {
        ICotacaoFxRepository repo = Substitute.For<ICotacaoFxRepository>();
        repo.GetMaisRecenteAsync(Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                                 Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((CotacaoFx?)null);
        return repo;
    }

    /// <summary>
    /// Cria um contrato ativo em USD com o valor principal informado.
    /// </summary>
    private static Contrato CriarContratoUsd(decimal valorUsd)
    {
        IClock clock = CriarClock();
        return Contrato.Criar(
            numeroExterno: $"CT-{Guid.NewGuid():N}",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(valorUsd, Moeda.Usd),
            dataContratacao: new LocalDate(2025, 1, 1),
            dataVencimento: new LocalDate(2027, 12, 31),
            taxaAa: Percentual.DeFracao(0.05m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock);
    }

    /// <summary>
    /// Cria um instrumento de hedge NDF Forward em USD com o notional informado.
    /// </summary>
    private static InstrumentoHedge CriarHedgeUsd(Guid contratoId, decimal notionalUsd)
    {
        IClock clock = CriarClock();
        return InstrumentoHedge.CriarForward(
            contratoId: contratoId,
            contraparteId: Guid.NewGuid(),
            notional: new Money(notionalUsd, Moeda.Usd),
            dataCont: new LocalDate(2025, 1, 1),
            dataVenc: new LocalDate(2027, 12, 31),
            strikeForward: 5.00m,
            clock: clock);
    }

    /// <summary>
    /// Constrói o handler com todas as dependências configuradas.
    /// </summary>
    private static GetHedgeEfetividadeQueryHandler CriarHandler(
        IContratoRepository contratoRepo,
        IHedgeRepository hedgeRepo,
        ICotacaoSpotCache? spotCache = null,
        ICotacaoFxRepository? fxRepo = null)
    {
        return new GetHedgeEfetividadeQueryHandler(
            contratoRepo: contratoRepo,
            hedgeRepo: hedgeRepo,
            spotCache: spotCache ?? CriarSpotCacheUsd(),
            cotacaoFxRepo: fxRepo ?? CriarFxRepoVazio(),
            clock: CriarClock());
    }

    // ── Teste 1: hedge parcial → TaxaCobertura ≈ 80 % ─────────────────────────

    [Fact]
    public async Task Handle_hedgeParcialUsd_taxaCoberturaPct80()
    {
        // Arrange
        const decimal notionalContrato = 100_000m; // 100k USD
        const decimal notionalHedge    =  80_000m; //  80k USD → 80% cobertura

        Contrato contrato = CriarContratoUsd(notionalContrato);
        InstrumentoHedge hedge = CriarHedgeUsd(contrato.Id, notionalHedge);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contrato }.AsReadOnly());

        IHedgeRepository hedgeRepo = Substitute.For<IHedgeRepository>();
        hedgeRepo.ListAtivosAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<InstrumentoHedge> { hedge }.AsReadOnly());
        hedgeRepo.GetSnapshotMaisRecenteAsync(hedge.Id, Arg.Any<CancellationToken>())
                 .Returns((PosicaoSnapshot?)null);

        GetHedgeEfetividadeQueryHandler handler = CriarHandler(contratoRepo, hedgeRepo);

        // Act
        EnvelopeResponse<HedgeEfetividadeDto> resposta =
            await handler.Handle(new GetHedgeEfetividadeQuery(), CancellationToken.None);

        // Assert
        // ExposicaoBRL = 100_000 × 5.10 = 510_000; CoberturaHedgeBRL = 80_000 × 5.10 = 408_000
        // TaxaCoberturaTotal = 408_000 / 510_000 * 100 = 80%
        resposta.Data.TaxaCoberturaPct.Should().BeApproximately(80m, precision: 0.01m,
            "hedge de 80k sobre exposição de 100k resulta em 80% de cobertura");

        HedgeEfetividadeMoedaDto linhaUsd = resposta.Data.PorMoeda
            .Single(m => m.Moeda == "USD");

        linhaUsd.TaxaCoberturaPct.Should().BeApproximately(80m, precision: 0.01m);
    }

    // ── Teste 2: sem hedge → TaxaCoberturaTotal = 0 ────────────────────────────

    [Fact]
    public async Task Handle_semHedge_taxaCoberturaPctZero()
    {
        // Arrange
        Contrato contrato = CriarContratoUsd(100_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contrato }.AsReadOnly());

        IHedgeRepository hedgeRepo = Substitute.For<IHedgeRepository>();
        hedgeRepo.ListAtivosAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<InstrumentoHedge>().AsReadOnly());

        GetHedgeEfetividadeQueryHandler handler = CriarHandler(contratoRepo, hedgeRepo);

        // Act
        EnvelopeResponse<HedgeEfetividadeDto> resposta =
            await handler.Handle(new GetHedgeEfetividadeQuery(), CancellationToken.None);

        // Assert
        resposta.Data.TaxaCoberturaPct.Should().Be(0m,
            "sem instrumentos de hedge a cobertura é zero");

        resposta.Data.CoberturaHedgeBrl.Should().Be(0m);

        HedgeEfetividadeMoedaDto linhaUsd = resposta.Data.PorMoeda
            .Single(m => m.Moeda == "USD");

        linhaUsd.TaxaCoberturaPct.Should().Be(0m);
        linhaUsd.Instrumentos.Should().BeEmpty();
    }

    // ── Teste 3: ExposicaoTotal = 0 → sem divisão por zero ────────────────────

    [Fact]
    public async Task Handle_exposicaoTotalZero_naoDivideERetornaZero()
    {
        // Arrange — nenhum contrato ativo; portanto exposição = 0
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato>().AsReadOnly());

        IHedgeRepository hedgeRepo = Substitute.For<IHedgeRepository>();
        hedgeRepo.ListAtivosAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<InstrumentoHedge>().AsReadOnly());

        GetHedgeEfetividadeQueryHandler handler = CriarHandler(contratoRepo, hedgeRepo);

        // Act — não deve lançar DivideByZeroException
        EnvelopeResponse<HedgeEfetividadeDto> resposta =
            await handler.Handle(new GetHedgeEfetividadeQuery(), CancellationToken.None);

        // Assert
        resposta.Data.ExposicaoTotalBrl.Should().Be(0m);
        resposta.Data.TaxaCoberturaPct.Should().Be(0m,
            "divisão por zero deve ser protegida retornando 0");
        resposta.Data.PorMoeda.Should().BeEmpty();
    }

    // ── Teste 4: AjusteMtmTotal = soma dos MtM de todos os instrumentos ────────

    [Fact]
    public async Task Handle_doisInstrumentos_ajusteMtmTotalESomaDosIndividuais()
    {
        // Arrange
        Contrato contrato = CriarContratoUsd(200_000m);

        InstrumentoHedge hedge1 = CriarHedgeUsd(contrato.Id, 100_000m);
        InstrumentoHedge hedge2 = CriarHedgeUsd(contrato.Id,  80_000m);

        const decimal mtmHedge1 =  1_000m; // BRL — positivo
        const decimal mtmHedge2 = -2_500m; // BRL — negativo

        IClock clock = CriarClock();

        PosicaoSnapshot snapshot1 = PosicaoSnapshot.CriarComInstant(
            hedgeId: hedge1.Id,
            contratoId: contrato.Id,
            mtmBrl: mtmHedge1,
            spotUtilizado: SpotUsd,
            tipoCotacao: "SPOT_INTRADAY",
            calculadoEm: InstanteFixo);

        PosicaoSnapshot snapshot2 = PosicaoSnapshot.CriarComInstant(
            hedgeId: hedge2.Id,
            contratoId: contrato.Id,
            mtmBrl: mtmHedge2,
            spotUtilizado: SpotUsd,
            tipoCotacao: "SPOT_INTRADAY",
            calculadoEm: InstanteFixo);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contrato }.AsReadOnly());

        IHedgeRepository hedgeRepo = Substitute.For<IHedgeRepository>();
        hedgeRepo.ListAtivosAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<InstrumentoHedge> { hedge1, hedge2 }.AsReadOnly());
        hedgeRepo.GetSnapshotMaisRecenteAsync(hedge1.Id, Arg.Any<CancellationToken>())
                 .Returns(snapshot1);
        hedgeRepo.GetSnapshotMaisRecenteAsync(hedge2.Id, Arg.Any<CancellationToken>())
                 .Returns(snapshot2);

        GetHedgeEfetividadeQueryHandler handler = CriarHandler(contratoRepo, hedgeRepo);

        // Act
        EnvelopeResponse<HedgeEfetividadeDto> resposta =
            await handler.Handle(new GetHedgeEfetividadeQuery(), CancellationToken.None);

        // Assert
        const decimal mtmEsperado = mtmHedge1 + mtmHedge2; // 1_000 − 2_500 = −1_500
        resposta.Data.AjusteMtmTotalBrl.Should().Be(mtmEsperado,
            "AjusteMtmTotal deve ser a soma algébrica dos MtM individuais");
    }

    // ── Teste 5: envelope com Completude = Completo quando cotações disponíveis ──

    [Fact]
    public async Task Handle_cotacoesDisponiveis_envelopeCompletudeCompleto()
    {
        // Arrange
        Contrato contrato = CriarContratoUsd(50_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contrato }.AsReadOnly());

        IHedgeRepository hedgeRepo = Substitute.For<IHedgeRepository>();
        hedgeRepo.ListAtivosAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<InstrumentoHedge>().AsReadOnly());

        GetHedgeEfetividadeQueryHandler handler = CriarHandler(contratoRepo, hedgeRepo);

        // Act
        EnvelopeResponse<HedgeEfetividadeDto> resposta =
            await handler.Handle(new GetHedgeEfetividadeQuery(), CancellationToken.None);

        // Assert
        resposta.Meta.Completude.Should().Be(Completude.Completo,
            "quando todas as moedas têm cotação disponível a completude deve ser Completo");
    }
}

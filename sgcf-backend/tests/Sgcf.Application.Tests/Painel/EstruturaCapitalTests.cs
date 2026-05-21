using FluentAssertions;
using MediatR;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Common;
using Sgcf.Application.Contabilidade;
using Sgcf.Application.Painel;
using Sgcf.Application.Painel.Queries;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contabilidade;
using Sgcf.Domain.Painel;
using Xunit;

namespace Sgcf.Application.Tests.Painel;

/// <summary>
/// Testes unitários para <see cref="GetEstruturaCapitalQueryHandler"/>.
/// Verifica cálculo de ICR, tratamento de divisão por zero e sinalização de dados ausentes.
/// GAP-CKP-04 / Task 1.3.
/// </summary>
[Trait("Category", "Slow")]
public sealed class EstruturaCapitalTests
{
    /// <summary>Instante fixo: 19/05/2026 09:00 UTC → BRT = 19/05/2026 06:00.</summary>
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 19, 9, 0);

    // ── Fábrica central ────────────────────────────────────────────────────────

    private static GetEstruturaCapitalQueryHandler CriarHandler(
        IMediator? mediator = null,
        IEbitdaMensalRepository? ebitdaRepo = null,
        IDadosContabeisRepository? contabeisRepo = null,
        IClock? clock = null)
    {
        return new GetEstruturaCapitalQueryHandler(
            mediator ?? CriarMediatorComDividaZero(),
            ebitdaRepo ?? CriarEbitdaRepoVazio(),
            contabeisRepo ?? CriarContabeisRepoVazio(),
            clock ?? CriarClock());
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static IClock CriarClock()
    {
        IClock c = Substitute.For<IClock>();
        c.GetCurrentInstant().Returns(InstanteFixo);
        return c;
    }

    private static IMediator CriarMediatorComDividaZero()
    {
        IMediator m = Substitute.For<IMediator>();
        m.Send(Arg.Any<GetPainelDividaQuery>(), Arg.Any<CancellationToken>())
         .Returns(new PainelDividaDto(
             DataHoraCalculo: InstanteFixo.ToString(),
             TipoCotacao: "SPOT_INTRADAY",
             BreakdownPorMoeda: new List<LinhaBreakdownMoedaDto>().AsReadOnly(),
             DividaBrutaBrl: 0m,
             AjusteMtm: new AjusteMtmDto(0m, 0m, 0m),
             DividaLiquidaPosHedgeBrl: 0m,
             Alertas: new List<string>().AsReadOnly()));
        return m;
    }

    private static IMediator CriarMediatorComDivida(decimal dividaBrutaBrl)
    {
        IMediator m = Substitute.For<IMediator>();
        m.Send(Arg.Any<GetPainelDividaQuery>(), Arg.Any<CancellationToken>())
         .Returns(new PainelDividaDto(
             DataHoraCalculo: InstanteFixo.ToString(),
             TipoCotacao: "SPOT_INTRADAY",
             BreakdownPorMoeda: new List<LinhaBreakdownMoedaDto>().AsReadOnly(),
             DividaBrutaBrl: dividaBrutaBrl,
             AjusteMtm: new AjusteMtmDto(0m, 0m, 0m),
             DividaLiquidaPosHedgeBrl: dividaBrutaBrl,
             Alertas: new List<string>().AsReadOnly()));
        return m;
    }

    private static IEbitdaMensalRepository CriarEbitdaRepoVazio()
    {
        IEbitdaMensalRepository r = Substitute.For<IEbitdaMensalRepository>();
        r.ListUltimos12MesesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
         .Returns(new List<EbitdaMensal>().AsReadOnly());
        return r;
    }

    private static IEbitdaMensalRepository CriarEbitdaRepo(decimal valorBrl)
    {
        // Cria um EbitdaMensal via factory — respeitando as regras de domínio.
        IClock clock = CriarClock();
        EbitdaMensal ebitda = EbitdaMensal.Criar(2026, 4, valorBrl, "test", clock);

        IEbitdaMensalRepository r = Substitute.For<IEbitdaMensalRepository>();
        r.ListUltimos12MesesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
         .Returns(new List<EbitdaMensal> { ebitda }.AsReadOnly());
        return r;
    }

    private static IDadosContabeisRepository CriarContabeisRepoVazio()
    {
        IDadosContabeisRepository r = Substitute.For<IDadosContabeisRepository>();
        r.ListUltimos12MesesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
         .Returns(new List<DadosContabeisMensal>().AsReadOnly());
        return r;
    }

    private static IDadosContabeisRepository CriarContabeisRepo(decimal plBrl, decimal despesaBrl)
    {
        IClock clock = CriarClock();
        Money pl = new(plBrl, Moeda.Brl);
        Money despesa = new(despesaBrl, Moeda.Brl);
        DadosContabeisMensal dados = DadosContabeisMensal.Criar(2026, 4, pl, despesa, clock);

        IDadosContabeisRepository r = Substitute.For<IDadosContabeisRepository>();
        r.ListUltimos12MesesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
         .Returns(new List<DadosContabeisMensal> { dados }.AsReadOnly());
        return r;
    }

    // ── Testes ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// EBITDA=1000 / DespesaFinanceira=200 → ICR = 5.
    /// </summary>
    [Fact]
    public async Task Handle_ComEbitdaE_DespesaFinanceira_RetornaIcrCalculado()
    {
        // Arrange
        GetEstruturaCapitalQueryHandler handler = CriarHandler(
            ebitdaRepo: CriarEbitdaRepo(1_000m),
            contabeisRepo: CriarContabeisRepo(plBrl: 500_000m, despesaBrl: 200m));

        // Act
        EnvelopeResponse<EstruturaCapitalDto> resultado =
            await handler.Handle(new GetEstruturaCapitalQuery(), CancellationToken.None);

        // Assert
        resultado.Data.EbitdaUltimos12mBrl.Should().Be(1_000m);
        resultado.Data.DespesaFinanceira12mBrl.Should().Be(200m);
        resultado.Data.Icr.Should().Be(5m);
        resultado.Data.Completude.Should().Be(Completude.Completo);
        resultado.Data.Alertas.Should().BeEmpty();
    }

    /// <summary>
    /// Sem registros de dados contábeis → completude Parcial, alerta DADOS_CONTABEIS_AUSENTES.
    /// </summary>
    [Fact]
    public async Task Handle_SemDadosContabeis_RetornaCompletudeParcialComAlerta()
    {
        // Arrange — contabeisRepo vazio, ebitdaRepo com valor
        GetEstruturaCapitalQueryHandler handler = CriarHandler(
            ebitdaRepo: CriarEbitdaRepo(500m),
            contabeisRepo: CriarContabeisRepoVazio());

        // Act
        EnvelopeResponse<EstruturaCapitalDto> resultado =
            await handler.Handle(new GetEstruturaCapitalQuery(), CancellationToken.None);

        // Assert
        resultado.Data.Completude.Should().Be(Completude.Parcial);
        resultado.Data.Alertas.Should().ContainSingle()
            .Which.Should().Be("DADOS_CONTABEIS_AUSENTES");
        resultado.Meta.Completude.Should().Be(Completude.Parcial);
    }

    /// <summary>
    /// DespesaFinanceira = 0 → ICR = 0 (não divide por zero).
    /// </summary>
    [Fact]
    public async Task Handle_ComDespesaFinanceiraZero_RetornaIcrZero()
    {
        // Arrange
        GetEstruturaCapitalQueryHandler handler = CriarHandler(
            ebitdaRepo: CriarEbitdaRepo(800m),
            contabeisRepo: CriarContabeisRepo(plBrl: 1_000_000m, despesaBrl: 0m));

        // Act
        EnvelopeResponse<EstruturaCapitalDto> resultado =
            await handler.Handle(new GetEstruturaCapitalQuery(), CancellationToken.None);

        // Assert
        resultado.Data.Icr.Should().Be(0m);
        resultado.Data.Completude.Should().Be(Completude.Completo);
    }

    /// <summary>
    /// Sem EBITDA e sem dados contábeis → ICR = 0, completude Parcial.
    /// </summary>
    [Fact]
    public async Task Handle_SemNenhumDado_RetornaZerosECompletudeParcial()
    {
        // Arrange — todos os repos vazios
        GetEstruturaCapitalQueryHandler handler = CriarHandler();

        // Act
        EnvelopeResponse<EstruturaCapitalDto> resultado =
            await handler.Handle(new GetEstruturaCapitalQuery(), CancellationToken.None);

        // Assert
        resultado.Data.Icr.Should().Be(0m);
        resultado.Data.EbitdaUltimos12mBrl.Should().Be(0m);
        resultado.Data.DespesaFinanceira12mBrl.Should().Be(0m);
        resultado.Data.Completude.Should().Be(Completude.Parcial);
        resultado.Data.Alertas.Should().Contain("DADOS_CONTABEIS_AUSENTES");
    }

    /// <summary>
    /// Dívida/PL calculado corretamente quando PL está disponível.
    /// </summary>
    [Fact]
    public async Task Handle_ComPlDisponivel_RetornaDividaSobrePatrimonio()
    {
        // Arrange
        GetEstruturaCapitalQueryHandler handler = CriarHandler(
            mediator: CriarMediatorComDivida(600_000m),
            contabeisRepo: CriarContabeisRepo(plBrl: 200_000m, despesaBrl: 10_000m));

        // Act
        EnvelopeResponse<EstruturaCapitalDto> resultado =
            await handler.Handle(new GetEstruturaCapitalQuery(), CancellationToken.None);

        // Assert
        resultado.Data.DividaTotalBrl.Should().Be(600_000m);
        resultado.Data.PatrimonioLiquidoBrl.Should().Be(200_000m);
        resultado.Data.DividaSobrePatrimonio.Should().Be(3m);
    }

    /// <summary>
    /// PL zero → DividaSobrePatrimonio = 0 (sem divisão por zero).
    /// </summary>
    [Fact]
    public async Task Handle_ComPlZero_RetornaDividaSobrePatrimonioZero()
    {
        // Arrange
        GetEstruturaCapitalQueryHandler handler = CriarHandler(
            mediator: CriarMediatorComDivida(500_000m),
            contabeisRepo: CriarContabeisRepo(plBrl: 0m, despesaBrl: 5_000m));

        // Act
        EnvelopeResponse<EstruturaCapitalDto> resultado =
            await handler.Handle(new GetEstruturaCapitalQuery(), CancellationToken.None);

        // Assert
        resultado.Data.DividaSobrePatrimonio.Should().Be(0m);
    }

    /// <summary>
    /// Meta do envelope deve conter as três fontes consultadas.
    /// </summary>
    [Fact]
    public async Task Handle_RetornaMetaComFontesConsultadas()
    {
        // Arrange
        GetEstruturaCapitalQueryHandler handler = CriarHandler(
            ebitdaRepo: CriarEbitdaRepo(100m),
            contabeisRepo: CriarContabeisRepo(50_000m, 20m));

        // Act
        EnvelopeResponse<EstruturaCapitalDto> resultado =
            await handler.Handle(new GetEstruturaCapitalQuery(), CancellationToken.None);

        // Assert
        resultado.Meta.FontesConsultadas.Should().HaveCount(3);
        resultado.Meta.FontesConsultadas
            .Select(f => f.Fonte)
            .Should().Contain(["contratos", "ebitda_mensal", "dados_contabeis_mensal"]);
    }
}

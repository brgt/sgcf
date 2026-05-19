using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Simulacao.Dtos;
using Sgcf.Application.Simulacao.Queries;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao.Queries;

/// <summary>
/// Testes unitários para <see cref="SimularCronogramaHipoteticoQueryHandler"/>.
/// Verificam que o handler delega corretamente ao <see cref="SimulacaoCronogramaCalculator"/>
/// e produz o DTO de sumário correto.
///
/// Todos os testes são puros — sem I/O, sem banco, sem clock real.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SimularCronogramaHipoteticoQueryHandlerTests
{
    // ── Clock fixo: 2026-05-19 → garante que datas a partir de 2026-07 são futuras ──

    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 19, 9, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    // ── Inputs de teste pré-configurados ──────────────────────────────────────────

    /// <summary>Input Bullet em USD com taxa fixa.</summary>
    private static AdicionarSimulacaoInput InputBullet() => new(
        BancoId: Guid.NewGuid(),
        Modalidade: "Finimp",
        Moeda: "Usd",
        ValorPrincipal: 1_000_000m,
        DataContratacaoPrevista: new DateOnly(2026, 9, 1),
        DataPrimeiroVencimento: new DateOnly(2027, 9, 1),
        TipoTaxa: "Fixa",
        TaxaAa: 6m,
        SpreadAa: null,
        BaseCalculo: "Dias360",
        EstruturaAmortizacao: "Bullet",
        Periodicidade: "Anual",
        QuantidadeParcelas: 1,
        AnchorDiaMes: "DiaContratacao");

    /// <summary>Input NCE em BRL com CDI+spread.</summary>
    private static AdicionarSimulacaoInput InputCdiSpread() => new(
        BancoId: Guid.NewGuid(),
        Modalidade: "Nce",
        Moeda: "Brl",
        ValorPrincipal: 500_000m,
        DataContratacaoPrevista: new DateOnly(2026, 8, 1),
        DataPrimeiroVencimento: new DateOnly(2027, 8, 1),
        TipoTaxa: "CdiSpread",
        TaxaAa: null,
        SpreadAa: 2m,
        BaseCalculo: "Dias360",
        EstruturaAmortizacao: "Bullet",
        Periodicidade: "Anual",
        QuantidadeParcelas: 1,
        AnchorDiaMes: "DiaContratacao");

    private static SimularCronogramaHipoteticoQueryHandler CriarHandler() =>
        new(CriarClock());

    // ── Teste 1: Bullet retorna eventos idênticos ao calculator ──────────────────

    /// <summary>
    /// Garante que o handler produz exatamente os mesmos eventos que
    /// <see cref="SimulacaoCronogramaCalculator.Calcular"/> produziria diretamente.
    /// Esta é a garantia AD-4 para o endpoint de preview.
    /// </summary>
    [Fact]
    public async Task Handle_Bullet_retornaEventosIdenticosAoCalculator()
    {
        // Arrange
        SimularCronogramaHipoteticoQueryHandler handler = CriarHandler();
        SimularCronogramaHipoteticoQuery query = new(InputBullet());

        // Act
        CronogramaHipoteticoDto resultado = await handler.Handle(query, CancellationToken.None);

        // Assert: reconstruir a simulação e comparar via calculator diretamente
        SimulacaoContratacao simulacaoEsperada = SimularCronogramaHipoteticoQueryHandler.ConstruirSimulacao(
            InputBullet(), CriarClock());
        IReadOnlyList<EventoCronogramaGerado> eventosEsperados =
            SimulacaoCronogramaCalculator.Calcular(simulacaoEsperada);

        resultado.Eventos.Should().HaveCount(eventosEsperados.Count);
        for (int i = 0; i < eventosEsperados.Count; i++)
        {
            resultado.Eventos[i].Numero.Should().Be(eventosEsperados[i].NumeroEvento);
            resultado.Eventos[i].Tipo.Should().Be(eventosEsperados[i].Tipo.ToString());
            resultado.Eventos[i].Valor.Should().Be(eventosEsperados[i].Valor.Valor);
        }
    }

    // ── Teste 2: CDI+spread aceita cdiReferencia e retorna taxa efetiva usada ───

    /// <summary>
    /// Para TipoTaxa.CdiSpread, o handler deve aceitar cdiReferenciaAaPercentual
    /// e expor a taxa efetiva composta no DTO de retorno.
    /// </summary>
    [Fact]
    public async Task Handle_CdiSpread_aceitaCdiReferenciaERetornaTaxaEfetivaUsada()
    {
        // Arrange
        const decimal cdiReferencia = 10.50m;
        SimularCronogramaHipoteticoQueryHandler handler = CriarHandler();
        SimularCronogramaHipoteticoQuery query = new(InputCdiSpread(), cdiReferencia);

        // Act
        CronogramaHipoteticoDto resultado = await handler.Handle(query, CancellationToken.None);

        // Assert: taxa efetiva composta = (1+0.1050)*(1+0.02)-1 = 12.71%
        decimal taxaEsperada = ((1m + 0.1050m) * (1m + 0.02m) - 1m) * 100m;
        resultado.TaxaEfetivaAaPercentual.Should().BeApproximately(taxaEsperada, 0.001m,
            because: "CDI+Spread deve usar composição (1+CDI)*(1+spread)-1 conforme convenção de mercado");
        resultado.Eventos.Should().NotBeEmpty();
    }

    // ── Teste 3: CDI+spread sem CdiReferencia lança ArgumentException ────────────

    /// <summary>
    /// Quando TipoTaxa = CdiSpread e CdiReferenciaAaPercentual não for informado,
    /// o handler deve propagar o ArgumentException do calculator.
    /// O controller mapeia ArgumentException → 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task Handle_CdiSpread_semCdi_lancaArgumentException()
    {
        // Arrange
        SimularCronogramaHipoteticoQueryHandler handler = CriarHandler();
        SimularCronogramaHipoteticoQuery query = new(InputCdiSpread(), CdiReferenciaAaPercentual: null);

        // Act
        Func<Task> ato = () => handler.Handle(query, CancellationToken.None);

        // Assert: ArgumentException com mensagem sobre CDI
        await ato.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cdiReferencia*");
    }

    // ── Teste 4: invariante I-1 violada lança ArgumentException ──────────────────

    /// <summary>
    /// ValorPrincipal = 0 viola o invariante I-1 de SimulacaoContratacao.
    /// Deve lançar ArgumentException — mapeado para 400 pelo controller.
    /// </summary>
    [Fact]
    public async Task Handle_invariantes_dominio_violados_lancaArgumentException()
    {
        // Arrange — valor principal zero viola I-1
        AdicionarSimulacaoInput inputInvalido = InputBullet() with { ValorPrincipal = 0m };
        SimularCronogramaHipoteticoQueryHandler handler = CriarHandler();
        SimularCronogramaHipoteticoQuery query = new(inputInvalido);

        // Act
        Func<Task> ato = () => handler.Handle(query, CancellationToken.None);

        // Assert
        await ato.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*principal*");
    }

    // ── Teste 5: sumário com QuantidadeEventos, PrincipalTotal e JurosTotal ─────

    /// <summary>
    /// O DTO de retorno deve incluir sumário financeiro correto:
    /// total de eventos, soma dos eventos de principal e soma dos juros.
    /// </summary>
    [Fact]
    public async Task Handle_retornaSumario_quantidadeEventos_PrincipalTotal_JurosTotal()
    {
        // Arrange — Price mensal 12 parcelas para garantir múltiplos eventos
        AdicionarSimulacaoInput inputPrice = new(
            BancoId: Guid.NewGuid(),
            Modalidade: "Nce",
            Moeda: "Brl",
            ValorPrincipal: 120_000m,
            DataContratacaoPrevista: new DateOnly(2026, 8, 1),
            DataPrimeiroVencimento: new DateOnly(2026, 9, 1),
            TipoTaxa: "Fixa",
            TaxaAa: 12m,
            SpreadAa: null,
            BaseCalculo: "Dias360",
            EstruturaAmortizacao: "Price",
            Periodicidade: "Mensal",
            QuantidadeParcelas: 12,
            AnchorDiaMes: "DiaContratacao");

        SimularCronogramaHipoteticoQueryHandler handler = CriarHandler();
        SimularCronogramaHipoteticoQuery query = new(inputPrice);

        // Act
        CronogramaHipoteticoDto resultado = await handler.Handle(query, CancellationToken.None);

        // Assert
        resultado.QuantidadeEventos.Should().Be(resultado.Eventos.Count);
        resultado.QuantidadeEventos.Should().BeGreaterThan(0);

        decimal principalTotal = resultado.Eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Principal.ToString())
            .Sum(e => e.Valor);
        decimal jurosTotal = resultado.Eventos
            .Where(e => e.Tipo == TipoEventoCronograma.Juros.ToString())
            .Sum(e => e.Valor);

        resultado.PrincipalTotal.Should().Be(principalTotal);
        resultado.JurosTotal.Should().Be(jurosTotal);

        // Invariante: soma de principal == ValorPrincipal
        principalTotal.Should().BeApproximately(120_000m, 1m,
            because: "a soma das parcelas de principal deve igualar o valor original");
    }
}

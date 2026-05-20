using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Simulacao;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Simulacao;
using Xunit;

namespace Sgcf.Application.Tests.Simulacao;

/// <summary>
/// Testes unitários para <see cref="SimulacaoCronogramaCalculator"/>.
/// Verificam que o calculador produz cronogramas idênticos ao motor de cronograma
/// real quando recebe os mesmos parâmetros. Isso é a garantia AD-4.
///
/// Todos os testes são puros — sem I/O, sem banco, sem clock real.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SimulacaoCronogramaCalculatorTests
{
    // ── Clock fixo: 2026-05-01 → garante que datas a partir de 2026-07 são futuras ──

    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 1, 9, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    // ── Fábrica de simulações de teste ──────────────────────────────────────────

    private static SimulacaoContratacao CriarSimulacaoBullet() =>
        SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            moeda: Moeda.Usd,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacaoPrevista: new LocalDate(2026, 9, 1),
            dataPrimeiroVencimento: new LocalDate(2027, 9, 1),
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(6m),
            spreadAa: null,
            baseCalculo: BaseCalculo.Dias360,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Anual,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock());

    private static SimulacaoContratacao CriarSimulacaoBulletComJuros() =>
        SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            moeda: Moeda.Usd,
            valorPrincipal: new Money(2_000_000m, Moeda.Usd),
            dataContratacaoPrevista: new LocalDate(2026, 7, 1),
            dataPrimeiroVencimento: new LocalDate(2027, 1, 1),
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(5.5m),
            spreadAa: null,
            baseCalculo: BaseCalculo.Dias360,
            estruturaAmortizacao: EstruturaAmortizacao.BulletComJurosPeriodicos,
            periodicidade: Periodicidade.Semestral,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock());

    private static SimulacaoContratacao CriarSimulacaoPrice() =>
        SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(500_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 8, 1),
            dataPrimeiroVencimento: new LocalDate(2026, 9, 1),
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(12m),
            spreadAa: null,
            baseCalculo: BaseCalculo.Dias360,
            estruturaAmortizacao: EstruturaAmortizacao.Price,
            periodicidade: Periodicidade.Mensal,
            quantidadeParcelas: 12,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock());

    private static SimulacaoContratacao CriarSimulacaoSac() =>
        SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Lei4131,
            moeda: Moeda.Usd,
            valorPrincipal: new Money(3_000_000m, Moeda.Usd),
            dataContratacaoPrevista: new LocalDate(2026, 7, 1),
            dataPrimeiroVencimento: new LocalDate(2027, 1, 1),
            tipoTaxa: TipoTaxa.Fixa,
            taxaAa: Percentual.De(4.5m),
            spreadAa: null,
            baseCalculo: BaseCalculo.Dias360,
            estruturaAmortizacao: EstruturaAmortizacao.Sac,
            periodicidade: Periodicidade.Semestral,
            quantidadeParcelas: 4,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock());

    private static SimulacaoContratacao CriarSimulacaoCdiSpread() =>
        SimulacaoContratacao.Criar(
            cenarioId: Guid.NewGuid(),
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Nce,
            moeda: Moeda.Brl,
            valorPrincipal: new Money(800_000m, Moeda.Brl),
            dataContratacaoPrevista: new LocalDate(2026, 8, 1),
            dataPrimeiroVencimento: new LocalDate(2027, 8, 1),
            tipoTaxa: TipoTaxa.CdiSpread,
            taxaAa: null,
            spreadAa: Percentual.De(2m),
            baseCalculo: BaseCalculo.Dias360,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidade: Periodicidade.Anual,
            quantidadeParcelas: 1,
            anchorDiaMes: AnchorDiaMes.DiaContratacao,
            anchorDiaFixo: null,
            garantiaExigidaPrevista: null,
            observacoes: null,
            clock: CriarClock());

    // ── Helpers de comparação ────────────────────────────────────────────────────

    /// <summary>
    /// Chama o motor real diretamente via GerarCronogramaInput + CronogramaStrategyFactory.
    /// Esta é a referência canônica que o calculador deve replicar bit-a-bit.
    /// </summary>
    private static IReadOnlyList<EventoCronogramaGerado> GerarViaMatorReal(
        SimulacaoContratacao simulacao,
        Percentual taxaEfetiva)
    {
        GerarCronogramaInput input = new(
            ValorPrincipal: simulacao.ValorPrincipal,
            TaxaAa: taxaEfetiva,
            BaseCalculo: simulacao.BaseCalculo,
            DataDesembolso: simulacao.DataContratacaoPrevista,
            DataPrimeiroVencimento: simulacao.DataPrimeiroVencimento,
            QuantidadeParcelas: simulacao.QuantidadeParcelas,
            Periodicidade: simulacao.Periodicidade,
            AnchorDiaMes: simulacao.AnchorDiaMes,
            AnchorDiaFixo: simulacao.AnchorDiaFixo,
            PeriodicidadeJuros: simulacao.EstruturaAmortizacao == EstruturaAmortizacao.BulletComJurosPeriodicos
                ? simulacao.Periodicidade
                : null,
            ConvencaoDataNaoUtil: ConvencaoDataNaoUtil.Following);

        return CronogramaStrategyFactory.Criar(simulacao.EstruturaAmortizacao).Gerar(input);
    }

    // ── Teste 1: Bullet ─────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_Bullet_geraEventosIdenticosAoMotorReal()
    {
        SimulacaoContratacao simulacao = CriarSimulacaoBullet();

        IReadOnlyList<EventoCronogramaGerado> resultado = SimulacaoCronogramaCalculator.Calcular(simulacao);

        IReadOnlyList<EventoCronogramaGerado> esperado = GerarViaMatorReal(simulacao, simulacao.TaxaAa!.Value);

        resultado.Should().BeEquivalentTo(esperado, opts => opts.WithStrictOrdering());
    }

    // ── Teste 2: BulletComJurosPeriodicos ───────────────────────────────────────

    [Fact]
    public void Calcular_BulletComJuros_geraEventosIdenticosAoMotorReal()
    {
        SimulacaoContratacao simulacao = CriarSimulacaoBulletComJuros();

        IReadOnlyList<EventoCronogramaGerado> resultado = SimulacaoCronogramaCalculator.Calcular(simulacao);

        IReadOnlyList<EventoCronogramaGerado> esperado = GerarViaMatorReal(simulacao, simulacao.TaxaAa!.Value);

        resultado.Should().BeEquivalentTo(esperado, opts => opts.WithStrictOrdering());
    }

    // ── Teste 3: Price ──────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_Price_geraEventosIdenticosAoMotorReal()
    {
        SimulacaoContratacao simulacao = CriarSimulacaoPrice();

        IReadOnlyList<EventoCronogramaGerado> resultado = SimulacaoCronogramaCalculator.Calcular(simulacao);

        IReadOnlyList<EventoCronogramaGerado> esperado = GerarViaMatorReal(simulacao, simulacao.TaxaAa!.Value);

        resultado.Should().BeEquivalentTo(esperado, opts => opts.WithStrictOrdering());
    }

    // ── Teste 4: SAC ────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_SAC_geraEventosIdenticosAoMotorReal()
    {
        SimulacaoContratacao simulacao = CriarSimulacaoSac();

        IReadOnlyList<EventoCronogramaGerado> resultado = SimulacaoCronogramaCalculator.Calcular(simulacao);

        IReadOnlyList<EventoCronogramaGerado> esperado = GerarViaMatorReal(simulacao, simulacao.TaxaAa!.Value);

        resultado.Should().BeEquivalentTo(esperado, opts => opts.WithStrictOrdering());
    }

    // ── Teste 5: CDI+Spread converte em taxa efetiva composta ───────────────────

    /// <summary>
    /// Decisão de implementação (AD-4 esclarecimento):
    /// CDI+Spread usa fórmula composta: taxa_efetiva = (1+cdi) × (1+spread) - 1.
    /// Exemplo: CDI=10.50% a.a. + Spread=2% a.a. → efetiva ≈ 12.71% a.a.
    /// (não aditiva: 10.50 + 2 = 12.50%, mas composta = 12.71%).
    ///
    /// Razão: alinhamento com convenção de mercado brasileiro de CDI+spread
    /// em contratos de capital de giro e NCE, onde o spread se soma de forma composta
    /// sobre o acumulado diário do CDI.
    /// </summary>
    [Fact]
    public void Calcular_TipoTaxaCdiSpread_usaTaxaCdiReferenciaParaConverterEmTaxaEfetiva()
    {
        SimulacaoContratacao simulacao = CriarSimulacaoCdiSpread();

        // CDI de referência: 10.50% a.a.
        const decimal cdiReferenciaAaPercentual = 10.50m;

        // Fórmula composta: (1+0.1050)*(1+0.02)-1 = 1.1050*1.02-1 = 0.1271 = 12.71%
        decimal cdi = cdiReferenciaAaPercentual / 100m;
        decimal spread = simulacao.SpreadAa!.Value.AsDecimal;
        decimal efetivaDecimal = (1m + cdi) * (1m + spread) - 1m;
        Percentual taxaEfetiva = Percentual.DeFracao(efetivaDecimal);

        IReadOnlyList<EventoCronogramaGerado> resultado =
            SimulacaoCronogramaCalculator.Calcular(simulacao, cdiReferenciaAaPercentual);

        IReadOnlyList<EventoCronogramaGerado> esperado = GerarViaMatorReal(simulacao, taxaEfetiva);

        resultado.Should().BeEquivalentTo(esperado, opts => opts.WithStrictOrdering());
    }

    // ── Teste 6: Simulação arquivada ainda produz cronograma ────────────────────

    /// <summary>
    /// CenarioSimulacao.Arquivar() torna o cenário imutável (sem edições),
    /// mas SimulacaoCronogramaCalculator não tem acesso ao CenarioSimulacao —
    /// opera somente sobre SimulacaoContratacao. Portanto, cenários arquivados
    /// devem continuar produzindo cronogramas normalmente (para leitura histórica).
    /// </summary>
    [Fact]
    public void Calcular_simulacaoArquivada_aindaProduzCronograma()
    {
        // O calculador opera sobre SimulacaoContratacao diretamente.
        // Não há state de "arquivado" na SimulacaoContratacao — apenas no CenarioSimulacao.
        // Este teste confirma que a entidade filha por si só não bloqueia a geração.
        SimulacaoContratacao simulacao = CriarSimulacaoBullet();

        // Ato: chama o calculador normalmente (não há restrição de status aqui)
        IReadOnlyList<EventoCronogramaGerado> resultado = SimulacaoCronogramaCalculator.Calcular(simulacao);

        resultado.Should().NotBeEmpty();
        resultado.Should().Contain(e => e.Tipo == TipoEventoCronograma.Principal);
        resultado.Should().Contain(e => e.Tipo == TipoEventoCronograma.Juros);
    }

    // ── Teste 7: CdiSpread sem cdiReferenciaAaPercentual lança ArgumentException ─

    [Fact]
    public void Calcular_CdiSpreadSemCdiReferencia_lancaArgumentException()
    {
        SimulacaoContratacao simulacao = CriarSimulacaoCdiSpread();

        Action ato = () => SimulacaoCronogramaCalculator.Calcular(simulacao, cdiReferenciaAaPercentual: null);

        ato.Should().Throw<ArgumentException>()
            .WithMessage("*cdiReferenciaAaPercentual*");
    }

    // ── Teste 8: total Principal == ValorPrincipal (property) ───────────────────

    [Theory]
    [InlineData("Bullet")]
    [InlineData("Price")]
    [InlineData("SAC")]
    public void Calcular_totalPrincipalIgualValorPrincipal(string estrutura)
    {
        SimulacaoContratacao simulacao = estrutura switch
        {
            "Bullet" => CriarSimulacaoBullet(),
            "Price" => CriarSimulacaoPrice(),
            "SAC" => CriarSimulacaoSac(),
            _ => throw new ArgumentException($"Estrutura desconhecida: {estrutura}")
        };

        IReadOnlyList<EventoCronogramaGerado> resultado = SimulacaoCronogramaCalculator.Calcular(simulacao);

        decimal totalPrincipal = resultado
            .Where(e => e.Tipo == TipoEventoCronograma.Principal)
            .Sum(e => e.Valor.Valor);

        totalPrincipal.Should().Be(simulacao.ValorPrincipal.Valor,
            because: "a soma das parcelas de Principal deve ser igual ao ValorPrincipal original");
    }
}

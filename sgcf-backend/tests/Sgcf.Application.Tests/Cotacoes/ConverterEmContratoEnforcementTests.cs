using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Commands;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Exceptions;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes unitários do enforcement SC-04 no <see cref="ConverterEmContratoCommandHandler"/>.
/// Verifica a matriz: tipo de garantia (Percentual, ValorFixo, Aval) × cenário de cobertura
/// (completa, parcial, zero, excedente, não-obrigatório), mais regra SC-07 (sem LimiteBanco/
/// sem revisão vigente).
///
/// Todos os repos são mockados com NSubstitute — sem banco de dados.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConverterEmContratoEnforcementTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 25, 10, 0);
    private static readonly LocalDate DataContratacao = new(2026, 5, 20);
    private static readonly LocalDate DataVencimento = new(2027, 5, 20);

    // Instante usado para criar limites — deve ser anterior a DataContratacao (2026-05-20)
    // para que RevisaoVigenteEm(momentoContratacao) encontre a revisão vigente.
    private static readonly Instant InstanteLimite = Instant.FromUtc(2026, 1, 1, 10, 0);

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    private static IClock CriarClockLimite()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteLimite);
        return clock;
    }

    /// <summary>
    /// Cria uma cotação FINIMP em estado Aceita com proposta USD de R$500.000 BRL
    /// (100.000 USD × PTAX 5.00). ValorAlvoBrl = 500.000 BRL.
    /// </summary>
    private static (Cotacao Cotacao, Proposta Proposta) CriarCotacaoAceita(Guid bancoId)
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-SC04-TEST",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 365,
            dataAbertura: new LocalDate(2026, 5, 16),
            dataPtaxReferencia: new LocalDate(2026, 5, 15),
            ptaxUsadaUsdBrl: 5.00m,
            clock: clock);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: bancoId,
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(100_000m, Moeda.Usd),
            taxaAaPercentual: 6.5m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0.5m,
            prazoDias: 365,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Money(500_000m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: new LocalDate(2026, 5, 16));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        // Pré-popula CET (normalmente feito pelo CompararPropostasCommandHandler).
        proposta.AtualizarCacheCalculos(
            cetAaPercentual: 7.0m,
            valorTotalEstimadoBrl: new Money(530_000m, Moeda.Brl));

        return (cotacao, proposta);
    }

    /// <summary>
    /// Constrói o handler com todos os repositórios mockados.
    /// <paramref name="limiteBanco"/> pode ser null para simular SC-07.
    /// </summary>
    private static ConverterEmContratoCommandHandler CriarHandler(
        Cotacao cotacao,
        LimiteBanco? limiteBanco,
        bool globalPuro = false,
        decimal? limiteGlobalBrl = null)
    {
        IClock clock = CriarClock();

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IEconomiaRepository economiaRepo = Substitute.For<IEconomiaRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ILimiteGlobalBancoRepository limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        IGarantiaRepository garantiaRepo = Substitute.For<IGarantiaRepository>();

        cotacaoRepo
            .GetByIdWithPropostasAsync(cotacao.Id, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        limiteBancoRepo
            .GetVigenteByBancoModalidadeAsync(
                Arg.Any<Guid>(),
                Arg.Any<ModalidadeContrato>(),
                Arg.Any<LocalDate>(),
                Arg.Any<CancellationToken>())
            .Returns(limiteBanco);

        LimiteGlobalBanco? limiteGlobal = limiteGlobalBrl is { } lg
            ? LimiteGlobalBanco.Criar(Guid.NewGuid(), new Money(lg, Moeda.Brl), new LocalDate(2026, 1, 1), CriarClockLimite())
            : null;
        limiteGlobalRepo
            .GetVigenteByBancoAsync(Arg.Any<Guid>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        CdiSnapshot cdi = CdiSnapshot.Criar(
            new LocalDate(2026, 5, 15),
            cdiAaPercentual: 10.5m,
            clock);
        cdiRepo
            .GetMaisRecenteAsync(Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(cdi);

        limiteBancoRepo
            .GetByBancoModalidadeAsync(Arg.Any<Guid>(), Arg.Any<ModalidadeContrato>(), Arg.Any<CancellationToken>())
            .Returns((LimiteBanco?)null);

        contratoRepo
            .CountByAnoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        contratoRepo
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        IConversorModalidade conversorFinimp = Substitute.For<IConversorModalidade>();
        conversorFinimp.Modalidade.Returns(ModalidadeContrato.Finimp);
        conversorFinimp
            .CriarDetailAsync(Arg.Any<ConverterEmContratoContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                ConverterEmContratoContext ctx = callInfo.Arg<ConverterEmContratoContext>();
                FinimpDetail detail = FinimpDetail.Criar(
                    contratoId: ctx.ContratoCriado.Id,
                    rofNumero: null,
                    rofDataEmissao: null,
                    exportadorNome: null,
                    exportadorPais: null,
                    produtoImportado: null,
                    faturaReferencia: null,
                    incoterm: null,
                    breakFundingFeePercentual: null,
                    temMarketFlex: false,
                    clock: clock);
                return Task.FromResult<(Domain.Common.Entity, Domain.Common.Entity?)>((detail, null));
            });

        // Regime: banco real (a proposta sempre referencia um banco existente). A detecção
        // usa o serviço; por padrão PerModalidade (globalPuro=false). Os limites destes testes
        // têm disponível >> principal (500k), então LG-11 passa e o foco permanece nas garantias.
        var banco = Banco.Criar("B07", "Banco SC04 S.A.", "BancoSC04", CriarClock());
        if (globalPuro)
        {
            banco.DefinirRegimeLimite(RegimeLimiteBanco.GlobalPuro, CriarClock());
        }

        var bancoRepo = Substitute.For<Sgcf.Application.Bancos.IBancoRepository>();
        bancoRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(banco);

        var saldo = Substitute.For<IConsultaSaldoBanco>();
        saldo.BancoEmRegimePerModalityAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(!globalPuro);
        saldo.CalcularSaldoDevedorBancoAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(new Money(0m, Moeda.Brl));

        var tenantContext = Substitute.For<Sgcf.Application.Tenancy.ITenantContext>();

        return new ConverterEmContratoCommandHandler(
            cotacaoRepo,
            contratoRepo,
            economiaRepo,
            limiteBancoRepo,
            limiteGlobalRepo,
            cdiRepo,
            garantiaRepo,
            bancoRepo,
            saldo,
            tenantContext,
            [conversorFinimp],
            clock);
    }

    private static ConverterEmContratoCommand CriarComando(
        Guid cotacaoId,
        IReadOnlyList<GarantiaContratoInput>? garantias = null) =>
        new(
            CotacaoId: cotacaoId,
            NumeroExternoContrato: "FIN-SC04-TEST",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(DataContratacao.Year, DataContratacao.Month, DataContratacao.Day),
            DataVencimento: new DateOnly(DataVencimento.Year, DataVencimento.Month, DataVencimento.Day),
            TaxaAa: 6.5m,
            GarantiasContrato: garantias);

    /// <summary>
    /// Cria um LimiteBanco com revisão vigente contendo os itens informados.
    /// </summary>
    private static LimiteBanco CriarLimiteComItens(
        Guid bancoId,
        params GarantiaExigidaItemSpec[] itens)
    {
        return LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClockLimite(),
            garantiasExigidas: itens);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Grupo 1: SC-07 — sem LimiteBanco ou sem revisão → enforcement desligado
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SC-07: sem LimiteBanco por modalidade, o enforcement de garantias fica desligado.
    /// Sob LG-11, "operar sem LimiteBanco" só é válido no regime GlobalPuro (com limite global
    /// vigente) — então este cenário é exercitado com um banco GlobalPuro. A conversão completa
    /// sem GarantiaExigidaNaoCobertaException mesmo com garantias ausentes.
    /// </summary>
    [Fact]
    public async Task SC07_SemLimiteBanco_ConverteSemlancamentoDeExcecao()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        // GlobalPuro com limite global folgado (5M » principal 500k) e sem LimiteBanco.
        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, limiteBanco: null, globalPuro: true, limiteGlobalBrl: 5_000_000m);

        // Sem garantias declaradas e sem LimiteBanco → deve completar sem exceção.
        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// SC-07: LimiteBanco sem revisão de garantias vigente → enforcement desligado.
    /// </summary>
    [Fact]
    public async Task SC07_LimiteSemRevisao_ConverteSemlancamentoDeExcecao()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        // Limite sem GarantiaExigidaItemSpec → RevisaoGarantiasVigente == null.
        LimiteBanco limiteSemRevisao = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(2_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClock());

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limiteSemRevisao);

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Grupo 2: PercentualSobreLimite — 80% sobre 500.000 BRL = 400.000 BRL
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cobertura exata de um item PercentualSobreLimite: declarado = esperado → sem lacuna.
    /// Principal BRL = 100.000 USD × 5.00 PTAX = 500.000 BRL.
    /// Esperado = 80% × 500.000 = 400.000 BRL.
    /// </summary>
    [Fact]
    public async Task Percentual_CoberturaSuficiente_NaoLanca()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.AlienacaoFiduciaria, PercentualSobreLimite: 80m, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        var garantias = new[]
        {
            new GarantiaContratoInput("AlienacaoFiduciaria", ValorBrl: 400_000m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None);

        await act.Should().NotThrowAsync("cobertura exata de 400.000 satisfaz 80% de 500.000");
    }

    /// <summary>
    /// Cobertura excedente: declarado > esperado → sem lacuna (excesso é aceito).
    /// </summary>
    [Fact]
    public async Task Percentual_CoberturaExcedente_NaoLanca()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.AlienacaoFiduciaria, PercentualSobreLimite: 80m, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        var garantias = new[]
        {
            // 500.000 > 400.000 esperado — excesso aceitável.
            new GarantiaContratoInput("AlienacaoFiduciaria", ValorBrl: 500_000m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None);

        await act.Should().NotThrowAsync("cobertura acima do mínimo não deve ser rejeitada");
    }

    /// <summary>
    /// Cobertura parcial: declarado abaixo do esperado → lança GarantiaExigidaNaoCobertaException
    /// com ValorEsperadoBrl = 400.000 e ValorCobertoBrl = 100.000.
    /// </summary>
    [Fact]
    public async Task Percentual_CoberturaParcial_LancaComValoresCorretos()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.AlienacaoFiduciaria, PercentualSobreLimite: 80m, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        var garantias = new[]
        {
            new GarantiaContratoInput("AlienacaoFiduciaria", ValorBrl: 100_000m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        GarantiaExigidaNaoCobertaException ex = await Assert.ThrowsAsync<GarantiaExigidaNaoCobertaException>(
            () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None));

        ex.Lacunas.Should().HaveCount(1, "um item obrigatório com cobertura insuficiente");
        ex.Lacunas[0].Tipo.Should().Be("AlienacaoFiduciaria");
        ex.Lacunas[0].ValorEsperadoBrl.Should().Be(400_000m, "80% × 500.000 = 400.000");
        ex.Lacunas[0].ValorCobertoBrl.Should().Be(100_000m, "declarado 100.000");
        ex.Lacunas[0].Obrigatoria.Should().BeTrue();
    }

    /// <summary>
    /// Sem garantias declaradas com item obrigatório PercentualSobreLimite:
    /// ValorCobertoBrl deve ser 0 (nenhuma garantia do tipo informada).
    /// </summary>
    [Fact]
    public async Task Percentual_SemGarantiaDeclarada_LancaComCobertaZero()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.Sblc, PercentualSobreLimite: 50m, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        // Nenhuma garantia declarada.
        GarantiaExigidaNaoCobertaException ex = await Assert.ThrowsAsync<GarantiaExigidaNaoCobertaException>(
            () => handler.Handle(CriarComando(cotacao.Id, garantias: null), CancellationToken.None));

        ex.Lacunas.Should().HaveCount(1);
        ex.Lacunas[0].ValorEsperadoBrl.Should().Be(250_000m, "50% × 500.000 = 250.000");
        ex.Lacunas[0].ValorCobertoBrl.Should().Be(0m, "nenhuma garantia do tipo declarada");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Grupo 3: ValorFixoBrl
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cobertura exata de um item ValorFixoBrl: declarado = esperado → sem lacuna.
    /// </summary>
    [Fact]
    public async Task ValorFixo_CoberturaSuficiente_NaoLanca()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, null,
                ValorFixoBrl: new Money(200_000m, Moeda.Brl), Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        var garantias = new[]
        {
            new GarantiaContratoInput("CdbCativo", ValorBrl: 200_000m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None);

        await act.Should().NotThrowAsync("cobertura exata do valor fixo satisfaz o requisito");
    }

    /// <summary>
    /// Cobertura insuficiente de ValorFixoBrl: declarado menor que o fixo → lança com
    /// ValorEsperadoBrl = 200.000 e ValorCobertoBrl = 50.000.
    /// </summary>
    [Fact]
    public async Task ValorFixo_CoberturaParcial_LancaComValoresCorretos()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, null,
                ValorFixoBrl: new Money(200_000m, Moeda.Brl), Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        var garantias = new[]
        {
            new GarantiaContratoInput("CdbCativo", ValorBrl: 50_000m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        GarantiaExigidaNaoCobertaException ex = await Assert.ThrowsAsync<GarantiaExigidaNaoCobertaException>(
            () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None));

        ex.Lacunas.Should().HaveCount(1);
        ex.Lacunas[0].ValorEsperadoBrl.Should().Be(200_000m, "valor fixo é 200.000");
        ex.Lacunas[0].ValorCobertoBrl.Should().Be(50_000m, "declarado 50.000");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Grupo 4: Aval puro (sem percentual e sem valor fixo)
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Aval puro: cobertura satisfeita pela presença de qualquer Aval, independente do valor.
    /// Mesmo com ValorBrl = 1 o enforcement não rejeita.
    /// </summary>
    [Fact]
    public async Task AvalPuro_ComQualquerAvalDeclarado_NaoLanca()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            // Aval sem percentual e sem valor fixo = Aval puro
            new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        var garantias = new[]
        {
            // Valor simbólico — presença é suficiente para Aval puro.
            new GarantiaContratoInput("Aval", ValorBrl: 1m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None);

        await act.Should().NotThrowAsync("Aval puro é satisfeito pela presença de qualquer Aval");
    }

    /// <summary>
    /// Aval puro obrigatório sem nenhum Aval declarado: lança com ValorEsperadoBrl = null
    /// e ValorCobertoBrl = null (caso especial documentado na SPEC §4.4).
    /// </summary>
    [Fact]
    public async Task AvalPuro_SemAvalDeclarado_LancaComNullsNaLacuna()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        // Garante outro tipo mas nenhum Aval.
        var garantias = new[]
        {
            new GarantiaContratoInput("CdbCativo", ValorBrl: 999_999m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        GarantiaExigidaNaoCobertaException ex = await Assert.ThrowsAsync<GarantiaExigidaNaoCobertaException>(
            () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None));

        ex.Lacunas.Should().HaveCount(1, "Aval obrigatório ausente deve gerar uma lacuna");
        ex.Lacunas[0].Tipo.Should().Be("Aval");
        ex.Lacunas[0].ValorEsperadoBrl.Should().BeNull("Aval puro não tem valor monetário esperado");
        ex.Lacunas[0].ValorCobertoBrl.Should().BeNull("sem garantia Aval declarada");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Grupo 5: Itens não-obrigatórios
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Itens com Obrigatoria = false NÃO devem gerar lacuna, mesmo sem cobertura.
    /// </summary>
    [Fact]
    public async Task ItemNaoObrigatorio_SemCobertura_NaoLanca()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            // Opcional — não bloqueia a conversão.
            new GarantiaExigidaItemSpec(TipoGarantia.Duplicatas, PercentualSobreLimite: 30m, null, Obrigatoria: false, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id, garantias: null), CancellationToken.None);

        await act.Should().NotThrowAsync("item não-obrigatório sem cobertura não deve bloquear a conversão");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Grupo 6: Múltiplos itens — combinações
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dois itens obrigatórios: Percentual (AlienacaoFiduciaria 80%) + Aval puro.
    /// Ambos cobertos → sem lacuna.
    /// </summary>
    [Fact]
    public async Task MultiploItens_TodosCobertos_NaoLanca()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.AlienacaoFiduciaria, PercentualSobreLimite: 80m, null, Obrigatoria: true, null),
            new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        var garantias = new[]
        {
            new GarantiaContratoInput("AlienacaoFiduciaria", ValorBrl: 400_000m, DataConstituicao: new DateOnly(2026, 5, 20)),
            new GarantiaContratoInput("Aval", ValorBrl: 1m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None);

        await act.Should().NotThrowAsync("todos os itens obrigatórios cobertos");
    }

    /// <summary>
    /// Dois itens obrigatórios: Percentual coberto, Aval puro ausente.
    /// Deve lançar com apenas 1 lacuna (Aval).
    /// </summary>
    [Fact]
    public async Task MultiploItens_UmNaoCoberto_LancaApenasALacunaCorreta()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.AlienacaoFiduciaria, PercentualSobreLimite: 80m, null, Obrigatoria: true, null),
            new GarantiaExigidaItemSpec(TipoGarantia.Aval, null, null, Obrigatoria: true, null));

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        // Cobre AlienacaoFiduciaria mas não Aval.
        var garantias = new[]
        {
            new GarantiaContratoInput("AlienacaoFiduciaria", ValorBrl: 400_000m, DataConstituicao: new DateOnly(2026, 5, 20))
        };

        GarantiaExigidaNaoCobertaException ex = await Assert.ThrowsAsync<GarantiaExigidaNaoCobertaException>(
            () => handler.Handle(CriarComando(cotacao.Id, garantias), CancellationToken.None));

        ex.Lacunas.Should().HaveCount(1, "apenas Aval está sem cobertura");
        ex.Lacunas[0].Tipo.Should().Be("Aval");
    }

    /// <summary>
    /// LimiteBancoId e GarantiasExigidasRevisaoId na exceção devem corresponder
    /// ao LimiteBanco e revisão vigente usados no enforcement.
    /// </summary>
    [Fact]
    public async Task ExcecaoLancada_CarregaIdsCorretos()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limite = CriarLimiteComItens(bancoId,
            new GarantiaExigidaItemSpec(TipoGarantia.CdbCativo, null,
                ValorFixoBrl: new Money(100_000m, Moeda.Brl), Obrigatoria: true, null));

        GarantiaExigidaRevisao? revisaoVigente = limite.RevisaoGarantiasVigente;
        revisaoVigente.Should().NotBeNull("teste requer revisão vigente");

        ConverterEmContratoCommandHandler handler = CriarHandler(cotacao, limite);

        GarantiaExigidaNaoCobertaException ex = await Assert.ThrowsAsync<GarantiaExigidaNaoCobertaException>(
            () => handler.Handle(CriarComando(cotacao.Id, garantias: null), CancellationToken.None));

        ex.LimiteBancoId.Should().Be(limite.Id, "deve referenciar o LimiteBanco que tem a política");
        ex.GarantiasExigidasRevisaoId.Should().Be(revisaoVigente!.Id, "deve referenciar a revisão vigente");
    }
}

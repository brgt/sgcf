using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Enforcement LG-11 no <see cref="ConverterEmContratoCommandHandler"/> para bancos em regime
/// <see cref="RegimeLimiteBanco.PerModalidade"/>: exigência de LimiteBanco, disponível da
/// modalidade, teto global agregado (opcional) e caminho feliz. SPEC_REGIME_LIMITE_EXPLICITO §4.4.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConverterEmContratoLG11Tests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 6, 2, 10, 0);
    private static readonly LocalDate DataContratacao = new(2026, 6, 2);
    private static readonly LocalDate DataVencimento = new(2027, 6, 2);
    private static readonly Instant InstanteLimite = Instant.FromUtc(2026, 1, 1, 10, 0);

    private static IClock CriarClock() => CriarClockComInstante(InstanteFixo);

    private static IClock CriarClockComInstante(Instant instante)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(instante);
        return clock;
    }

    private static (Cotacao Cotacao, Proposta Proposta) CriarCotacaoAceita(Guid bancoId)
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-LG11-TEST",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 365,
            dataAbertura: new LocalDate(2026, 5, 30),
            dataPtaxReferencia: new LocalDate(2026, 5, 29),
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
            dataCaptura: new LocalDate(2026, 5, 30));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        proposta.AtualizarCacheCalculos(
            cetAaPercentual: 7.5m,
            valorTotalEstimadoBrl: new Money(532_500m, Moeda.Brl));

        return (cotacao, proposta);
    }

    private static Banco CriarBancoPerModalidade() =>
        Banco.Criar("B02", "Banco Teste Per-Modalidade S.A.", "BancoPM", CriarClock());

    private static LimiteBanco CriarLimiteBancoComDisponivel(
        Guid bancoId, decimal valorLimiteBrl, decimal valorUtilizadoBrl)
    {
        IClock clockLimite = CriarClockComInstante(InstanteLimite);

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(valorLimiteBrl, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clockLimite);

        if (valorUtilizadoBrl > 0m)
        {
            limite.RegistrarUso(new Money(valorUtilizadoBrl, Moeda.Brl), clockLimite);
        }

        return limite;
    }

    private static LimiteGlobalBanco CriarLimiteGlobal(Guid bancoId, decimal valorLimiteBrl) =>
        LimiteGlobalBanco.Criar(
            bancoId: bancoId,
            valorLimiteBrl: new Money(valorLimiteBrl, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClock());

    private static ConverterEmContratoCommandHandler CriarHandler(
        Cotacao cotacao,
        Banco? banco,
        LimiteBanco? limiteBanco,
        LimiteGlobalBanco? limiteGlobal,
        decimal utilizadoAgregadoBrl = 0m)
    {
        IClock clock = CriarClock();

        ICotacaoRepository cotacaoRepo = Substitute.For<ICotacaoRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IEconomiaRepository economiaRepo = Substitute.For<IEconomiaRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ILimiteGlobalBancoRepository limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        IGarantiaRepository garantiaRepo = Substitute.For<IGarantiaRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        IConsultaSaldoBanco saldo = Substitute.For<IConsultaSaldoBanco>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();

        cotacaoRepo
            .GetByIdWithPropostasAsync(cotacao.Id, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        limiteBancoRepo
            .GetVigenteByBancoModalidadeAsync(
                Arg.Any<Guid>(), Arg.Any<ModalidadeContrato>(),
                Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteBanco);

        limiteBancoRepo
            .GetByBancoModalidadeAsync(Arg.Any<Guid>(), Arg.Any<ModalidadeContrato>(), Arg.Any<CancellationToken>())
            .Returns((LimiteBanco?)null);

        if (limiteBanco is not null)
        {
            limiteBancoRepo
                .GetByIdTrackingAsync(limiteBanco.Id, Arg.Any<CancellationToken>())
                .Returns(limiteBanco);
        }

        limiteGlobalRepo
            .GetVigenteByBancoAsync(Arg.Any<Guid>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        bancoRepo
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(banco);

        tenantContext.TenantId.Returns(Guid.NewGuid());

        saldo
            .CalcularUtilizadoAgregadoModalidadesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Money(utilizadoAgregadoBrl, Moeda.Brl));

        CdiSnapshot cdi = CdiSnapshot.Criar(
            new LocalDate(2026, 6, 1),
            cdiAaPercentual: 10.5m,
            clock);
        cdiRepo
            .GetMaisRecenteAsync(Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(cdi);

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

    private static ConverterEmContratoCommand CriarComando(Guid cotacaoId) =>
        new(
            CotacaoId: cotacaoId,
            NumeroExternoContrato: "FIN-LG11-TEST",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(DataContratacao.Year, DataContratacao.Month, DataContratacao.Day),
            DataVencimento: new DateOnly(DataVencimento.Year, DataVencimento.Month, DataVencimento.Day),
            TaxaAa: 6.5m);

    [Fact]
    public async Task RegimePerModalidade_SemLimiteBancoNaModalidade_Bloqueia()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoPerModalidade();

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteBanco: null, limiteGlobal: null, utilizadoAgregadoBrl: 0m);

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        ex.Message.Should().Contain("[LG-11]");
        ex.Message.Should().Contain("regime per-modalidade");
        ex.Message.Should().Contain("LimiteBanco");
    }

    [Fact]
    public async Task RegimePerModalidade_ExcedeDisponivel_Bloqueia()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoPerModalidade();

        // Disponível = 1.000.000 - 700.000 = 300.000 < 500.000 (principal)
        LimiteBanco limiteBanco = CriarLimiteBancoComDisponivel(bancoId,
            valorLimiteBrl: 1_000_000m, valorUtilizadoBrl: 700_000m);

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteBanco, limiteGlobal: null);

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        ex.Message.Should().Contain("[LG-11]");
        ex.Message.Should().Contain("disponível", "deve indicar disponibilidade insuficiente na modalidade");
    }

    [Fact]
    public async Task RegimePerModalidade_ExcedeTetoGlobal_Bloqueia()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoPerModalidade();

        // Disponível na modalidade suficiente (5M), mas teto global agregado estourado.
        LimiteBanco limiteBanco = CriarLimiteBancoComDisponivel(bancoId,
            valorLimiteBrl: 5_000_000m, valorUtilizadoBrl: 0m);
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(bancoId, valorLimiteBrl: 2_000_000m);

        // 1.600.000 + 500.000 = 2.100.000 > 2.000.000
        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteBanco, limiteGlobal, utilizadoAgregadoBrl: 1_600_000m);

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        ex.Message.Should().Contain("[LG-11]");
        ex.Message.Should().Contain("teto global");
    }

    [Fact]
    public async Task RegimePerModalidade_SemLimiteGlobal_IgnoraChecagemGlobal_Converte()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoPerModalidade();

        LimiteBanco limiteBanco = CriarLimiteBancoComDisponivel(bancoId,
            valorLimiteBrl: 5_000_000m, valorUtilizadoBrl: 0m);

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteBanco, limiteGlobal: null, utilizadoAgregadoBrl: 0m);

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);
        await act.Should().NotThrowAsync(
            "sem LimiteGlobal vigente o teto agregado não é verificado no regime per-modalidade");
    }

    [Fact]
    public async Task RegimePerModalidade_DentroDeAmbosOsTetos_RegistraUsoEConverte()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoPerModalidade();

        LimiteBanco limiteBanco = CriarLimiteBancoComDisponivel(bancoId,
            valorLimiteBrl: 5_000_000m, valorUtilizadoBrl: 0m);
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(bancoId, valorLimiteBrl: 2_000_000m);

        // 1.000.000 + 500.000 = 1.500.000 ≤ 2.000.000
        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteBanco, limiteGlobal, utilizadoAgregadoBrl: 1_000_000m);

        decimal utilizadoAntes = limiteBanco.ValorUtilizadoBrl.Valor;

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);
        await act.Should().NotThrowAsync("todas as checagens de teto estão satisfeitas");

        // Principal BRL = 100.000 USD × PTAX 5.00 = 500.000 BRL.
        limiteBanco.ValorUtilizadoBrl.Valor.Should().Be(utilizadoAntes + 500_000m,
            "RegistrarUso deve ter sido chamado com o principal de 500.000 BRL");
    }
}

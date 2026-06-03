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
/// Enforcement LG-12 no <see cref="ConverterEmContratoCommandHandler"/> para bancos em regime
/// <see cref="RegimeLimiteBanco.GlobalPuro"/>. SPEC_REGIME_LIMITE_EXPLICITO §4.4.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConverterEmContratoRegimeGlobalTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 6, 1, 10, 0);
    private static readonly LocalDate DataContratacao = new(2026, 6, 1);
    private static readonly LocalDate DataVencimento = new(2027, 6, 1);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        return clock;
    }

    private static (Cotacao Cotacao, Proposta Proposta) CriarCotacaoAceita(Guid bancoId)
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-LG12-TEST",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 365,
            dataAbertura: new LocalDate(2026, 5, 28),
            dataPtaxReferencia: new LocalDate(2026, 5, 27),
            ptaxUsadaUsdBrl: 5.00m,
            clock: clock);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: bancoId,
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(100_000m, Moeda.Usd),
            taxaAaPercentual: 7.0m,
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
            dataCaptura: new LocalDate(2026, 5, 28));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        proposta.AtualizarCacheCalculos(
            cetAaPercentual: 8.0m,
            valorTotalEstimadoBrl: new Money(535_000m, Moeda.Brl));

        return (cotacao, proposta);
    }

    private static Banco CriarBancoGlobalPuro()
    {
        IClock clock = CriarClock();
        Banco banco = Banco.Criar("B01", "Banco Teste Global Puro S.A.", "BancoGP", clock);
        banco.DefinirRegimeLimite(RegimeLimiteBanco.GlobalPuro, clock);
        return banco;
    }

    private static LimiteGlobalBanco CriarLimiteGlobal(Guid bancoId, decimal valorLimiteBrl)
    {
        return LimiteGlobalBanco.Criar(
            bancoId: bancoId,
            valorLimiteBrl: new Money(valorLimiteBrl, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClock());
    }

    private static ConverterEmContratoCommandHandler CriarHandler(
        Cotacao cotacao,
        Banco? banco,
        LimiteGlobalBanco? limiteGlobal,
        decimal saldoDevedorBrl = 0m)
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

        // Banco em regime GlobalPuro não tem LimiteBanco por modalidade.
        limiteBancoRepo
            .GetVigenteByBancoModalidadeAsync(
                Arg.Any<Guid>(), Arg.Any<ModalidadeContrato>(),
                Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns((LimiteBanco?)null);

        limiteBancoRepo
            .GetByBancoModalidadeAsync(Arg.Any<Guid>(), Arg.Any<ModalidadeContrato>(), Arg.Any<CancellationToken>())
            .Returns((LimiteBanco?)null);

        limiteGlobalRepo
            .GetVigenteByBancoAsync(Arg.Any<Guid>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        bancoRepo
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(banco);

        tenantContext.TenantId.Returns(Guid.NewGuid());

        saldo
            .CalcularSaldoDevedorBancoAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Money(saldoDevedorBrl, Moeda.Brl));

        CdiSnapshot cdi = CdiSnapshot.Criar(
            new LocalDate(2026, 5, 30),
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
            NumeroExternoContrato: "FIN-LG12-TEST",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(DataContratacao.Year, DataContratacao.Month, DataContratacao.Day),
            DataVencimento: new DateOnly(DataVencimento.Year, DataVencimento.Month, DataVencimento.Day),
            TaxaAa: 7.0m);

    [Fact]
    public async Task RegimeGlobal_DentroDoTeto_Converte()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoGlobalPuro();
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(bancoId, valorLimiteBrl: 2_000_000m);

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteGlobal, saldoDevedorBrl: 1_000_000m);

        // 1.000.000 + 500.000 = 1.500.000 ≤ 2.000.000
        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);
        await act.Should().NotThrowAsync("saldo devedor + principal está dentro do teto global");
    }

    [Fact]
    public async Task RegimeGlobal_EstourandoTeto_Bloqueia()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoGlobalPuro();
        LimiteGlobalBanco limiteGlobal = CriarLimiteGlobal(bancoId, valorLimiteBrl: 1_000_000m);

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteGlobal, saldoDevedorBrl: 600_000m);

        // 600.000 + 500.000 = 1.100.000 > 1.000.000
        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        ex.Message.Should().Contain("[LG-12]");
        ex.Message.Should().Contain("limite global");
        ex.Message.Should().Contain("600000");
        ex.Message.Should().Contain("500000");
        ex.Message.Should().Contain("1000000");
    }

    [Fact]
    public async Task RegimeGlobal_SemLimiteGlobalVigente_Bloqueia()
    {
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        Banco banco = CriarBancoGlobalPuro();

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, banco, limiteGlobal: null, saldoDevedorBrl: 0m);

        Func<Task> act = () => handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        ex.Message.Should().Contain("[REG-03]");
        ex.Message.Should().Contain("regime de limite global");
        ex.Message.Should().Contain("não possui limite global vigente");
    }
}

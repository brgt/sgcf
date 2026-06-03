using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Commands;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes unitários de T2.1 — verifica que <see cref="ConverterEmContratoCommandHandler"/>
/// preenche as 3 FKs de rastreabilidade (SC-01, SC-02, SC-03) no contrato criado.
/// Todos os repositórios são mockados com NSubstitute — sem dependência de banco de dados.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConverterEmContratoFKsTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 25, 12, 0);
    private static readonly LocalDate DataContratacao = new(2026, 5, 20);
    private static readonly LocalDate DataVencimento = new(2027, 5, 20);

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    /// <summary>
    /// Monta uma cotação em estado aceito com proposta FINIMP USD.
    /// </summary>
    private static (Cotacao Cotacao, Proposta Proposta) CriarCotacaoAceita(Guid bancoId)
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-T21",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 365,
            dataAbertura: new LocalDate(2026, 5, 16),
            dataPtaxReferencia: new LocalDate(2026, 5, 15),
            ptaxUsadaUsdBrl: 5.20m,
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
            valorGarantiaExigidaBrl: new Money(600_000m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: new LocalDate(2026, 5, 16));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        // O handler requer CetCalculadoAaPercentual preenchido na proposta aceita.
        // Em produção esse valor é setado pelo CompararPropostasCommandHandler.
        // Nos testes unitários do handler, pré-populamos diretamente via internal accessor.
        proposta.AtualizarCacheCalculos(cetAaPercentual: 7.2m, valorTotalEstimadoBrl: new Money(520_000m, Moeda.Brl));

        return (cotacao, proposta);
    }

    /// <summary>
    /// Cria um <see cref="LimiteBanco"/> com uma revisão de garantias vigente contendo 1 item.
    /// </summary>
    private static LimiteBanco CriarLimiteBancoComRevisao(Guid bancoId)
    {
        // Usa instante anterior a DataContratacao (2026-05-20) para que
        // RevisaoVigenteEm(momentoContratacao) localize a revisão criada aqui.
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 1, 1, 10, 0));

        LimiteBanco limite = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(2_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: clock,
            garantiasExigidas:
            [
                // Obrigatoria = false: o item cria a revisão (necessária para GarantiasExigidasRevisaoId)
                // mas não aciona o enforcement SC-04, que é testado em ConverterEmContratoEnforcementTests.
                new GarantiaExigidaItemSpec(TipoGarantia.AlienacaoFiduciaria, 80m, null, false, null)
            ]);

        return limite;
    }

    /// <summary>
    /// Monta o handler com todos os mocks necessários e configura os repos com os valores
    /// informados para o lookup de políticas vigentes.
    /// </summary>
    private static ConverterEmContratoCommandHandler CriarHandler(
        Cotacao cotacao,
        LimiteBanco? limiteBanco,
        LimiteGlobalBanco? limiteGlobal,
        out ICotacaoRepository cotacaoRepo,
        out IContratoRepository contratoRepo)
    {
        IClock clock = CriarClock();

        cotacaoRepo = Substitute.For<ICotacaoRepository>();
        contratoRepo = Substitute.For<IContratoRepository>();

        IEconomiaRepository economiaRepo = Substitute.For<IEconomiaRepository>();
        ILimiteBancoRepository limiteBancoRepo = Substitute.For<ILimiteBancoRepository>();
        ILimiteGlobalBancoRepository limiteGlobalRepo = Substitute.For<ILimiteGlobalBancoRepository>();
        ICdiSnapshotRepository cdiRepo = Substitute.For<ICdiSnapshotRepository>();
        IGarantiaRepository garantiaRepoMock = Substitute.For<IGarantiaRepository>();

        // Cotação retorna a cotação preparada com proposta aceita.
        cotacaoRepo
            .GetByIdWithPropostasAsync(cotacao.Id, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        // Lookup temporal de LimiteBanco (SC-01).
        limiteBancoRepo
            .GetVigenteByBancoModalidadeAsync(
                Arg.Any<Guid>(),
                Arg.Any<ModalidadeContrato>(),
                Arg.Any<LocalDate>(),
                Arg.Any<CancellationToken>())
            .Returns(limiteBanco);

        // Lookup de LimiteGlobal vigente (SC-02).
        limiteGlobalRepo
            .GetVigenteByBancoAsync(Arg.Any<Guid>(), Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(limiteGlobal);

        // CDI — necessário para CalcularCet no handler.
        CdiSnapshot cdi = CdiSnapshot.Criar(
            new LocalDate(2026, 5, 15),
            cdiAaPercentual: 10.5m,
            clock);
        cdiRepo
            .GetMaisRecenteAsync(Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
            .Returns(cdi);

        // LimiteBanco lookup usado para RegistrarUso (o existente GetByBancoModalidade).
        limiteBancoRepo
            .GetByBancoModalidadeAsync(Arg.Any<Guid>(), Arg.Any<ModalidadeContrato>(), Arg.Any<CancellationToken>())
            .Returns((LimiteBanco?)null);

        // contratoRepo.CountByAnoAsync para gerar codigo interno.
        contratoRepo
            .CountByAnoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // SaveChanges — sem efeito colateral.
        contratoRepo
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        // Conversor FINIMP — cria um FinimpDetail stub.
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

        var bancoRepo = Substitute.For<Sgcf.Application.Bancos.IBancoRepository>();
        var saldo = Substitute.For<IConsultaSaldoBanco>();
        var tenantContext = Substitute.For<Sgcf.Application.Tenancy.ITenantContext>();

        return new ConverterEmContratoCommandHandler(
            cotacaoRepo,
            contratoRepo,
            economiaRepo,
            limiteBancoRepo,
            limiteGlobalRepo,
            cdiRepo,
            garantiaRepoMock,
            bancoRepo,
            saldo,
            tenantContext,
            [conversorFinimp],
            clock);
    }

    private static ConverterEmContratoCommand CriarComando(Guid cotacaoId) =>
        new(
            CotacaoId: cotacaoId,
            NumeroExternoContrato: "FIN-2026-T21",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(DataContratacao.Year, DataContratacao.Month, DataContratacao.Day),
            DataVencimento: new DateOnly(DataVencimento.Year, DataVencimento.Month, DataVencimento.Day),
            TaxaAa: 6.5m);

    // ──────────────────────────────────────────────────────────────────────────
    // Testes
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Converter_ComLimiteBancoERevisaoAtiva_PreencheTresFKs()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);
        LimiteBanco limiteBanco = CriarLimiteBancoComRevisao(bancoId);
        LimiteGlobalBanco limiteGlobal = LimiteGlobalBanco.Criar(
            bancoId: bancoId,
            valorLimiteBrl: new Money(5_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClock());

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, limiteBanco, limiteGlobal,
            out _, out IContratoRepository contratoRepo);

        // Captura o Contrato passado para contratoRepo.Add
        Contrato? contratoCapturado = null;
        contratoRepo.When(r => r.Add(Arg.Any<Contrato>()))
            .Do(callInfo => contratoCapturado = callInfo.Arg<Contrato>());

        // Act
        ContratoDto dto = await handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        // Assert — as 3 FKs devem estar preenchidas
        GarantiaExigidaRevisao? revisaoVigente = limiteBanco.RevisaoGarantiasVigente;
        revisaoVigente.Should().NotBeNull("o teste exige revisão vigente");

        dto.LimiteBancoId.Should().Be(limiteBanco.Id);
        dto.LimiteGlobalBancoId.Should().Be(limiteGlobal.Id);
        dto.GarantiasExigidasRevisaoId.Should().Be(revisaoVigente!.Id);

        // Verifica estado interno do Contrato capturado (white-box via InternalsVisibleTo).
        contratoCapturado.Should().NotBeNull();
        contratoCapturado!.LimiteBancoId.Should().Be(limiteBanco.Id);
        contratoCapturado.LimiteGlobalBancoId.Should().Be(limiteGlobal.Id);
        contratoCapturado.GarantiasExigidasRevisaoId.Should().Be(revisaoVigente.Id);
    }

    [Fact]
    public async Task Converter_ComLimiteBancoSemRevisao_PreencheLimiteIdENuloRevisao()
    {
        // Arrange — limite sem garantias → sem revisão → RevisaoGarantiasVigente == null.
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteBanco limiteSemRevisao = LimiteBanco.Criar(
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorLimiteBrl: new Money(1_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClock());
        // Sem garantiasExigidas → RevisaoGarantiasVigente == null.

        LimiteGlobalBanco limiteGlobal = LimiteGlobalBanco.Criar(
            bancoId: bancoId,
            valorLimiteBrl: new Money(3_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClock());

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao, limiteSemRevisao, limiteGlobal,
            out _, out IContratoRepository contratoRepo);

        Contrato? contratoCapturado = null;
        contratoRepo.When(r => r.Add(Arg.Any<Contrato>()))
            .Do(callInfo => contratoCapturado = callInfo.Arg<Contrato>());

        // Act
        ContratoDto dto = await handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        // Assert — LimiteBancoId e LimiteGlobalBancoId preenchidos; revisão é null (SC-03 não aplicável).
        dto.LimiteBancoId.Should().Be(limiteSemRevisao.Id);
        dto.LimiteGlobalBancoId.Should().Be(limiteGlobal.Id);
        dto.GarantiasExigidasRevisaoId.Should().BeNull();

        contratoCapturado!.LimiteBancoId.Should().Be(limiteSemRevisao.Id);
        contratoCapturado.LimiteGlobalBancoId.Should().Be(limiteGlobal.Id);
        contratoCapturado.GarantiasExigidasRevisaoId.Should().BeNull();
    }

    [Fact]
    public async Task Converter_SemLimiteBancoCadastrado_PreencheFKsComoNull()
    {
        // Arrange — SC-07: banco sem LimiteBanco → todos os 3 ids ficam null.
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao,
            limiteBanco: null,
            limiteGlobal: null,
            out _, out IContratoRepository contratoRepo);

        Contrato? contratoCapturado = null;
        contratoRepo.When(r => r.Add(Arg.Any<Contrato>()))
            .Do(callInfo => contratoCapturado = callInfo.Arg<Contrato>());

        // Act — deve completar sem erro (SC-07 não lança exceção).
        ContratoDto dto = await handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        // Assert — todas as FKs devem ser null.
        dto.LimiteBancoId.Should().BeNull();
        dto.LimiteGlobalBancoId.Should().BeNull();
        dto.GarantiasExigidasRevisaoId.Should().BeNull();

        contratoCapturado!.LimiteBancoId.Should().BeNull();
        contratoCapturado.LimiteGlobalBancoId.Should().BeNull();
        contratoCapturado.GarantiasExigidasRevisaoId.Should().BeNull();
    }

    [Fact]
    public async Task Converter_ComLimiteGlobalSemLimitePorModalidade_PreencheApenasGlobalId()
    {
        // Arrange — banco tem LimiteGlobal mas não tem LimiteBanco para a modalidade.
        Guid bancoId = Guid.NewGuid();
        (Cotacao cotacao, _) = CriarCotacaoAceita(bancoId);

        LimiteGlobalBanco limiteGlobal = LimiteGlobalBanco.Criar(
            bancoId: bancoId,
            valorLimiteBrl: new Money(10_000_000m, Moeda.Brl),
            dataVigenciaInicio: new LocalDate(2026, 1, 1),
            clock: CriarClock());

        // LimiteBanco == null para este banco+modalidade.
        ConverterEmContratoCommandHandler handler = CriarHandler(
            cotacao,
            limiteBanco: null,
            limiteGlobal: limiteGlobal,
            out _, out IContratoRepository contratoRepo);

        Contrato? contratoCapturado = null;
        contratoRepo.When(r => r.Add(Arg.Any<Contrato>()))
            .Do(callInfo => contratoCapturado = callInfo.Arg<Contrato>());

        // Act
        ContratoDto dto = await handler.Handle(CriarComando(cotacao.Id), CancellationToken.None);

        // Assert — apenas LimiteGlobalBancoId preenchido; os outros dois null.
        dto.LimiteBancoId.Should().BeNull();
        dto.LimiteGlobalBancoId.Should().Be(limiteGlobal.Id);
        dto.GarantiasExigidasRevisaoId.Should().BeNull();

        contratoCapturado!.LimiteBancoId.Should().BeNull();
        contratoCapturado.LimiteGlobalBancoId.Should().Be(limiteGlobal.Id);
        contratoCapturado.GarantiasExigidasRevisaoId.Should().BeNull();
    }
}

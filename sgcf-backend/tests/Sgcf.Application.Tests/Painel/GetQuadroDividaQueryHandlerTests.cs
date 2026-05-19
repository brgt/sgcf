using FluentAssertions;
using MediatR;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Bancos;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Application.Painel.Queries;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;
using Xunit;

namespace Sgcf.Application.Tests.Painel;

/// <summary>
/// Testes unitários para <see cref="GetQuadroDividaQueryHandler"/>.
/// 8 cenários definidos na Fase 1 Task 1.4.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GetQuadroDividaQueryHandlerTests
{
    /// <summary>Instante fixo: 19/05/2026 09:00 UTC → ano corrente Brasília = 2026.</summary>
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 19, 9, 0);
    private const int AnoCorrente = 2026;

    // ── Fábrica central ────────────────────────────────────────────────────────

    private static GetQuadroDividaQueryHandler CriarHandler(
        IMediator? mediator = null,
        IContratoRepository? contratoRepo = null,
        IEventoCronogramaRepository? cronogramaRepo = null,
        IBancoRepository? bancoRepo = null,
        ICotacaoSpotCache? spotCache = null,
        ICotacaoFxRepository? cotacaoFxRepo = null,
        IClock? clock = null)
    {
        mediator ??= CriarMediatorComSaldo(CriarSaldoVazio());
        contratoRepo ??= CriarContratoRepoVazio();
        cronogramaRepo ??= CriarCronogramaRepoVazio();
        bancoRepo ??= CriarBancoRepoVazio();
        spotCache ??= Substitute.For<ICotacaoSpotCache>();
        cotacaoFxRepo ??= Substitute.For<ICotacaoFxRepository>();
        clock ??= CriarClock();

        return new GetQuadroDividaQueryHandler(
            mediator,
            contratoRepo,
            cronogramaRepo,
            bancoRepo,
            spotCache,
            cotacaoFxRepo,
            clock);
    }

    // ── Helpers de setup ──────────────────────────────────────────────────────

    private static IClock CriarClock()
    {
        IClock c = Substitute.For<IClock>();
        c.GetCurrentInstant().Returns(InstanteFixo);
        return c;
    }

    private static IMediator CriarMediatorComSaldo(SaldoPorBancoAtualDto saldo)
    {
        IMediator m = Substitute.For<IMediator>();
        m.Send(Arg.Any<GetSaldoPorBancoAtualQuery>(), Arg.Any<CancellationToken>())
         .Returns(saldo);
        return m;
    }

    private static IContratoRepository CriarContratoRepoVazio()
    {
        IContratoRepository r = Substitute.For<IContratoRepository>();
        r.ListAsync(Arg.Any<CancellationToken>())
         .Returns(new List<Contrato>().AsReadOnly());
        return r;
    }

    private static IEventoCronogramaRepository CriarCronogramaRepoVazio()
    {
        IEventoCronogramaRepository r = Substitute.For<IEventoCronogramaRepository>();
        r.ListAbertosParaAnoAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
         .Returns(new List<EventoCronograma>().AsReadOnly());
        return r;
    }

    private static IBancoRepository CriarBancoRepoVazio()
    {
        IBancoRepository r = Substitute.For<IBancoRepository>();
        r.ListAllAsync(Arg.Any<CancellationToken>())
         .Returns(new List<Banco>().AsReadOnly());
        return r;
    }

    private static Banco CriarBanco(string compe, string apelido)
    {
        IClock c = CriarClock();
        return Banco.Criar(
            codigoCompe: compe,
            razaoSocial: $"Banco {apelido} S.A.",
            apelido: apelido,
            padraoAntecipacao: PadraoAntecipacao.A,
            clock: c);
    }

    private static SaldoPorBancoAtualDto CriarSaldoVazio()
        => new(Bancos: [], SaldoTotalBrl: 0m, DataReferencia: new LocalDate(AnoCorrente, 5, 19));

    private static SaldoPorBancoAtualDto CriarSaldoUnicoBanco(Banco banco, decimal saldo)
        => new(
            Bancos: [new SaldoBancoAtualDto(
                BancoId: banco.Id,
                BancoApelido: banco.Apelido,
                BancoCodigoCompe: banco.CodigoCompe,
                SaldoBrl: saldo,
                QuantidadeContratosAtivos: 1)],
            SaldoTotalBrl: saldo,
            DataReferencia: new LocalDate(AnoCorrente, 5, 19));

    private static Contrato CriarContratoBrl(Guid bancoId, decimal valorPrincipal)
    {
        IClock c = CriarClock();
        return Contrato.Criar(
            numeroExterno: $"BRL-{Guid.NewGuid():N}",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(valorPrincipal, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 1),
            dataVencimento: new LocalDate(2028, 1, 1),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: c);
    }

    private static EventoCronograma CriarEventoPrincipal(Guid contratoId, LocalDate data, decimal valor)
        => EventoCronograma.Criar(
            contratoId: contratoId,
            numeroEvento: 1,
            tipo: TipoEventoCronograma.Principal,
            dataPrevista: data,
            valorMoedaOriginal: new Money(valor, Moeda.Brl));

    // ── Teste 1: ano sem contratos → 12 meses com valores zero ────────────────

    [Fact]
    public async Task Handle_anoSemContratos_retorna12MesesVazios()
    {
        // Arrange
        GetQuadroDividaQueryHandler handler = CriarHandler(
            mediator: CriarMediatorComSaldo(CriarSaldoVazio()),
            contratoRepo: CriarContratoRepoVazio(),
            cronogramaRepo: CriarCronogramaRepoVazio());

        // Act
        QuadroDividaDto resultado = await handler.Handle(
            new GetQuadroDividaQuery(AnoCorrente), CancellationToken.None);

        // Assert
        resultado.Ano.Should().Be(AnoCorrente);
        resultado.Projecao.Meses.Should().HaveCount(12);
        resultado.Projecao.Meses.Should().AllSatisfy(m =>
        {
            m.SaldoTotalInicio.Should().Be(0m);
            m.SaldoTotalFim.Should().Be(0m);
            m.TotalAmortizacaoMes.Should().Be(0m);
        });
        resultado.Sumario.SaldoTotalInicioAno.Should().Be(0m);
        resultado.Sumario.SaldoTotalFimAno.Should().Be(0m);
        resultado.Alertas.Should().BeEmpty();
    }

    // ── Teste 2: contrato ativo com amortizações no ano → saldo decrescente ──

    [Fact]
    public async Task Handle_contratoAtivoComAmortizacoesNoAno_projetaSaldoDecrescente()
    {
        // Arrange
        Banco banco = CriarBanco("001", "BancoTest");
        Contrato contrato = CriarContratoBrl(banco.Id, 1_200_000m);

        IMediator mediator = CriarMediatorComSaldo(CriarSaldoUnicoBanco(banco, 1_200_000m));

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contrato }.AsReadOnly());

        // 3 amortizações de 100k em março, junho e setembro
        IReadOnlyList<EventoCronograma> eventos =
        [
            CriarEventoPrincipal(contrato.Id, new LocalDate(AnoCorrente, 3, 15), 100_000m),
            CriarEventoPrincipal(contrato.Id, new LocalDate(AnoCorrente, 6, 15), 100_000m),
            CriarEventoPrincipal(contrato.Id, new LocalDate(AnoCorrente, 9, 15), 100_000m),
        ];

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo
            .ListAbertosParaAnoAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(eventos);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<Banco> { banco }.AsReadOnly());

        GetQuadroDividaQueryHandler handler = CriarHandler(
            mediator: mediator,
            contratoRepo: contratoRepo,
            cronogramaRepo: cronogramaRepo,
            bancoRepo: bancoRepo);

        // Act
        QuadroDividaDto resultado = await handler.Handle(
            new GetQuadroDividaQuery(AnoCorrente), CancellationToken.None);

        // Assert — saldos decrescentes após cada amortização
        resultado.Projecao.Meses[2].SaldoTotalFim.Should().Be(1_100_000m, "março: 1.2M - 100k = 1.1M");
        resultado.Projecao.Meses[5].SaldoTotalFim.Should().Be(1_000_000m, "junho: 1.1M - 100k = 1M");
        resultado.Projecao.Meses[8].SaldoTotalFim.Should().Be(900_000m, "setembro: 1M - 100k = 900k");

        resultado.Sumario.TotalAmortizacaoNoAno.Should().Be(300_000m);
        resultado.Sumario.SaldoTotalFimAno.Should().Be(900_000m);
    }

    // ── Teste 3: múltiplos bancos → breakdown por banco e mês correto ─────────

    [Fact]
    public async Task Handle_multiplosBancos_breakdownPorBancoEMesCorreto()
    {
        // Arrange
        Banco bancoA = CriarBanco("001", "BancoA");
        Banco bancoB = CriarBanco("002", "BancoB");

        Contrato contratoA = CriarContratoBrl(bancoA.Id, 2_000_000m);
        Contrato contratoB = CriarContratoBrl(bancoB.Id, 1_000_000m);

        SaldoPorBancoAtualDto snapshot = new(
            Bancos:
            [
                new SaldoBancoAtualDto(bancoA.Id, bancoA.Apelido, bancoA.CodigoCompe, 2_000_000m, 1),
                new SaldoBancoAtualDto(bancoB.Id, bancoB.Apelido, bancoB.CodigoCompe, 1_000_000m, 1),
            ],
            SaldoTotalBrl: 3_000_000m,
            DataReferencia: new LocalDate(AnoCorrente, 5, 19));

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contratoA, contratoB }.AsReadOnly());

        // Junho: BancoA amortiza 200k, BancoB amortiza 100k
        IReadOnlyList<EventoCronograma> eventos =
        [
            CriarEventoPrincipal(contratoA.Id, new LocalDate(AnoCorrente, 6, 1), 200_000m),
            CriarEventoPrincipal(contratoB.Id, new LocalDate(AnoCorrente, 6, 1), 100_000m),
        ];

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo
            .ListAbertosParaAnoAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(eventos);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<Banco> { bancoA, bancoB }.AsReadOnly());

        GetQuadroDividaQueryHandler handler = CriarHandler(
            mediator: CriarMediatorComSaldo(snapshot),
            contratoRepo: contratoRepo,
            cronogramaRepo: cronogramaRepo,
            bancoRepo: bancoRepo);

        // Act
        QuadroDividaDto resultado = await handler.Handle(
            new GetQuadroDividaQuery(AnoCorrente), CancellationToken.None);

        // Assert — junho (índice 5): total fim = 3M - 300k = 2.7M
        MesProjecaoDto junho = resultado.Projecao.Meses[5];
        junho.SaldoTotalFim.Should().Be(2_700_000m);

        SaldoBancoMesDto? bancoAMes = junho.Bancos.FirstOrDefault(b => b.BancoId == bancoA.Id);
        SaldoBancoMesDto? bancoBMes = junho.Bancos.FirstOrDefault(b => b.BancoId == bancoB.Id);

        bancoAMes.Should().NotBeNull();
        bancoAMes!.SaldoFim.Should().Be(1_800_000m);
        bancoAMes.TotalAmortizacaoNoMes.Should().Be(200_000m);
        bancoAMes.BancoApelido.Should().Be("BancoA");

        bancoBMes.Should().NotBeNull();
        bancoBMes!.SaldoFim.Should().Be(900_000m);
        bancoBMes.TotalAmortizacaoNoMes.Should().Be(100_000m);
        bancoBMes.BancoApelido.Should().Be("BancoB");
    }

    // ── Teste 4: amortização fora do ano consultado → ignorada ────────────────

    [Fact]
    public async Task Handle_amortizacaoForaDoAnoConsultado_eIgnorada()
    {
        // Arrange
        Banco banco = CriarBanco("001", "BancoFora");
        Contrato contrato = CriarContratoBrl(banco.Id, 1_000_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contrato }.AsReadOnly());

        // Evento em 2025 — fora do ano 2026 consultado
        IReadOnlyList<EventoCronograma> eventos =
        [
            CriarEventoPrincipal(contrato.Id, new LocalDate(2025, 12, 15), 500_000m),
        ];

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo
            .ListAbertosParaAnoAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(eventos);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<Banco> { banco }.AsReadOnly());

        GetQuadroDividaQueryHandler handler = CriarHandler(
            mediator: CriarMediatorComSaldo(CriarSaldoUnicoBanco(banco, 1_000_000m)),
            contratoRepo: contratoRepo,
            cronogramaRepo: cronogramaRepo,
            bancoRepo: bancoRepo);

        // Act
        QuadroDividaDto resultado = await handler.Handle(
            new GetQuadroDividaQuery(AnoCorrente), CancellationToken.None);

        // Assert — evento de 2025 é filtrado pelo ProjetorSaldoMensal (P-5)
        resultado.Projecao.Meses.Should().AllSatisfy(m =>
            m.SaldoTotalFim.Should().Be(1_000_000m));

        resultado.Sumario.TotalAmortizacaoNoAno.Should().Be(0m);
    }

    // ── Teste 5: invariante SnapshotInicial.SaldoTotalBrl == Projecao.Meses[0].SaldoTotalInicio ──

    [Fact]
    public async Task Handle_saldoInicialIgualPainelDivida_invariante()
    {
        // Arrange
        Banco banco = CriarBanco("001", "BancoInv");
        decimal saldoBrl = 5_000_000m;

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<Banco> { banco }.AsReadOnly());

        GetQuadroDividaQueryHandler handler = CriarHandler(
            mediator: CriarMediatorComSaldo(CriarSaldoUnicoBanco(banco, saldoBrl)),
            bancoRepo: bancoRepo);

        // Act
        QuadroDividaDto resultado = await handler.Handle(
            new GetQuadroDividaQuery(AnoCorrente), CancellationToken.None);

        // Assert — invariante AD-8: SnapshotInicial.SaldoTotalBrl == Projecao.Meses[0].SaldoTotalInicio
        resultado.SnapshotInicial.SaldoTotalBrl.Should().Be(saldoBrl);
        resultado.Projecao.Meses[0].SaldoTotalInicio.Should().Be(saldoBrl,
            "saldo inicial deve ser exatamente o início do primeiro mês projetado");
    }

    // ── Teste 6: ano inválido (fora 2020–2050) → lança ArgumentException ──────

    [Fact]
    public async Task Handle_anoInvalido_lancaArgumentException()
    {
        // Arrange
        GetQuadroDividaQueryHandler handler = CriarHandler();

        // Act & Assert
        await handler
            .Invoking(h => h.Handle(new GetQuadroDividaQuery(2019), CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*2019*");

        await handler
            .Invoking(h => h.Handle(new GetQuadroDividaQuery(2051), CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*2051*");
    }

    // ── Teste 7: ano corrente → delega snapshot para GetSaldoPorBancoAtualQuery ──

    [Fact]
    public async Task Handle_anoCorrente_usaPainelComoSaldoInicial()
    {
        // Arrange
        Banco banco = CriarBanco("001", "BancoAtual");
        SaldoPorBancoAtualDto saldoEsperado = CriarSaldoUnicoBanco(banco, 3_000_000m);
        IMediator mediator = CriarMediatorComSaldo(saldoEsperado);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<Banco> { banco }.AsReadOnly());

        GetQuadroDividaQueryHandler handler = CriarHandler(mediator: mediator, bancoRepo: bancoRepo);

        // Act
        QuadroDividaDto resultado = await handler.Handle(
            new GetQuadroDividaQuery(AnoCorrente), CancellationToken.None);

        // Assert — snapshot retornado pelo mediator é repassado no DTO
        resultado.SnapshotInicial.SaldoTotalBrl.Should().Be(3_000_000m);
        resultado.SnapshotInicial.Bancos.Should().HaveCount(1);
        resultado.SnapshotInicial.Bancos[0].BancoId.Should().Be(banco.Id);

        // Confirma que o handler chamou GetSaldoPorBancoAtualQuery via mediator
        await mediator.Received(1)
            .Send(Arg.Any<GetSaldoPorBancoAtualQuery>(), Arg.Any<CancellationToken>());
    }

    // ── Teste 8: ano != corrente → lança InvalidOperationException (MVP Q9) ──

    [Fact]
    public async Task Handle_anoNaoCorrente_lancaInvalidOperationException()
    {
        // Arrange — clock retorna 2026; pedindo 2025 (passado) ou 2027 (futuro)
        GetQuadroDividaQueryHandler handler = CriarHandler();

        // Ano passado
        await handler
            .Invoking(h => h.Handle(new GetQuadroDividaQuery(2025), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MVP*");

        // Ano futuro
        await handler
            .Invoking(h => h.Handle(new GetQuadroDividaQuery(2027), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MVP*");
    }

    // ── Teste extra: sumário calculado corretamente ───────────────────────────

    [Fact]
    public async Task Handle_comEventos_sumarioCalculadoCorretamente()
    {
        // Arrange
        Banco banco = CriarBanco("001", "BancoSum");
        Contrato contrato = CriarContratoBrl(banco.Id, 1_000_000m);

        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        contratoRepo.ListAsync(Arg.Any<CancellationToken>())
                    .Returns(new List<Contrato> { contrato }.AsReadOnly());

        IReadOnlyList<EventoCronograma> eventos =
        [
            CriarEventoPrincipal(contrato.Id, new LocalDate(AnoCorrente, 2, 1), 50_000m),
            CriarEventoPrincipal(contrato.Id, new LocalDate(AnoCorrente, 5, 1), 50_000m),
            CriarEventoPrincipal(contrato.Id, new LocalDate(AnoCorrente, 11, 1), 100_000m),
        ];

        IEventoCronogramaRepository cronogramaRepo = Substitute.For<IEventoCronogramaRepository>();
        cronogramaRepo
            .ListAbertosParaAnoAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(eventos);

        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        bancoRepo.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(new List<Banco> { banco }.AsReadOnly());

        GetQuadroDividaQueryHandler handler = CriarHandler(
            mediator: CriarMediatorComSaldo(CriarSaldoUnicoBanco(banco, 1_000_000m)),
            contratoRepo: contratoRepo,
            cronogramaRepo: cronogramaRepo,
            bancoRepo: bancoRepo);

        // Act
        QuadroDividaDto resultado = await handler.Handle(
            new GetQuadroDividaQuery(AnoCorrente), CancellationToken.None);

        // Assert — total amortizado = 200k; variação = -20%
        resultado.Sumario.TotalAmortizacaoNoAno.Should().Be(200_000m);
        resultado.Sumario.SaldoTotalInicioAno.Should().Be(1_000_000m);
        resultado.Sumario.SaldoTotalFimAno.Should().Be(800_000m);
        resultado.Sumario.VariacaoAnualPercentual.Should().BeApproximately(-20m, 0.001m);
    }
}

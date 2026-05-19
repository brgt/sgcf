using FluentAssertions;
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
using Xunit;

namespace Sgcf.Application.Tests.Painel;

[Trait("Category", "Unit")]
public sealed class GetSaldoPorBancoAtualQueryHandlerTests
{
    private static readonly Instant InstanteFixo = Instant.FromUtc(2026, 5, 19, 9, 0);

    // ── fábrica central do handler ──────────────────────────────────────────────

    private static GetSaldoPorBancoAtualQueryHandler CriarHandler(
        IContratoRepository contratoRepo,
        IBancoRepository? bancoRepo = null,
        ICotacaoSpotCache? spotCache = null,
        ICotacaoFxRepository? cotacaoFxRepo = null)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        bancoRepo ??= Substitute.For<IBancoRepository>();
        spotCache ??= Substitute.For<ICotacaoSpotCache>();
        cotacaoFxRepo ??= Substitute.For<ICotacaoFxRepository>();

        return new GetSaldoPorBancoAtualQueryHandler(
            contratoRepo,
            bancoRepo,
            spotCache,
            cotacaoFxRepo,
            clock);
    }

    /// <summary>
    /// Cria um Banco real via factory e registra-o no IBancoRepository stub.
    /// Retorna o Banco criado para que o caller use banco.Id ao criar contratos.
    /// </summary>
    private static Banco CriarBancoNoRepo(IBancoRepository bancoRepo, string compe, string apelido)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        Banco banco = Banco.Criar(
            codigoCompe: compe,
            razaoSocial: $"Banco {apelido} S.A.",
            apelido: apelido,
            padraoAntecipacao: PadraoAntecipacao.A,
            clock: clock);

        bancoRepo.GetByIdAsync(banco.Id, Arg.Any<CancellationToken>()).Returns(banco);
        return banco;
    }

    private static Contrato CriarContratoBrl(Guid bancoId, decimal valorPrincipal = 1_000_000m)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return Contrato.Criar(
            numeroExterno: $"BRL-{Guid.NewGuid():N}",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorPrincipal: new Money(valorPrincipal, Moeda.Brl),
            dataContratacao: new LocalDate(2025, 1, 1),
            dataVencimento: new LocalDate(2027, 1, 1),
            taxaAa: Percentual.DeFracao(0.10m),
            baseCalculo: BaseCalculo.Dias252,
            clock: clock);
    }

    private static Contrato CriarContratoUsd(Guid bancoId, decimal valorPrincipal = 500_000m)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);

        return Contrato.Criar(
            numeroExterno: $"USD-{Guid.NewGuid():N}",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(valorPrincipal, Moeda.Usd),
            dataContratacao: new LocalDate(2025, 1, 1),
            dataVencimento: new LocalDate(2027, 1, 1),
            taxaAa: Percentual.DeFracao(0.05m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    private static CotacaoFx CriarPtaxUsd(decimal compra, decimal venda)
        => CotacaoFx.Criar(
            moedaBase: Moeda.Usd,
            tipo: TipoCotacao.PtaxD1,
            valorCompra: new Money(compra, Moeda.Brl),
            valorVenda: new Money(venda, Moeda.Brl),
            fonte: "BCB",
            momento: InstanteFixo.Minus(Duration.FromDays(1)));

    // ── Teste 1: sem contratos retorna lista vazia ──────────────────────────────

    [Fact]
    public async Task Handle_semContratos_retornaListaVazia()
    {
        // Arrange
        IContratoRepository repo = Substitute.For<IContratoRepository>();
        repo.ListAsync(default).ReturnsForAnyArgs(new List<Contrato>().AsReadOnly());

        GetSaldoPorBancoAtualQueryHandler handler = CriarHandler(repo);

        // Act
        SaldoPorBancoAtualDto resultado = await handler.Handle(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Assert
        resultado.Bancos.Should().BeEmpty();
        resultado.SaldoTotalBrl.Should().Be(0m);
    }

    // ── Teste 2: contrato BRL retorna saldo sem conversão ──────────────────────

    [Fact]
    public async Task Handle_contratoBrl_retornaSaldoSemConversao()
    {
        // Arrange
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        Banco banco = CriarBancoNoRepo(bancoRepo, "001", "BancoTest");

        Contrato contrato = CriarContratoBrl(banco.Id, valorPrincipal: 2_000_000m);

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        repo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();

        GetSaldoPorBancoAtualQueryHandler handler = CriarHandler(repo, bancoRepo, spotCache, fxRepo);

        // Act
        SaldoPorBancoAtualDto resultado = await handler.Handle(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Assert
        resultado.Bancos.Should().HaveCount(1);
        SaldoBancoAtualDto saldoBanco = resultado.Bancos[0];
        saldoBanco.BancoId.Should().Be(banco.Id);
        saldoBanco.BancoApelido.Should().Be("BancoTest");
        saldoBanco.BancoCodigoCompe.Should().Be("001");
        saldoBanco.SaldoBrl.Should().Be(2_000_000m);
        saldoBanco.QuantidadeContratosAtivos.Should().Be(1);
        resultado.SaldoTotalBrl.Should().Be(2_000_000m);

        // Para BRL não deve consultar spot ou PTAX
        await spotCache.DidNotReceiveWithAnyArgs().GetSpotAsync(default, default);
        await fxRepo.DidNotReceiveWithAnyArgs().GetMaisRecenteAsync(default, default, default, default);
    }

    // ── Teste 3: contrato USD converte via PTAX quando spot ausente ────────────

    [Fact]
    public async Task Handle_contratoUsd_retornaSaldoConvertidoViaPtax()
    {
        // Arrange
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        Banco banco = CriarBancoNoRepo(bancoRepo, "002", "BancoUSD");

        Contrato contrato = CriarContratoUsd(banco.Id, valorPrincipal: 1_000_000m);

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        repo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        // Sem spot intraday
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
                 .Returns((Money?)null);

        // PTAX D-1: compra = 5,20 / venda = 5,22 → midrate = 5,21
        CotacaoFx ptax = CriarPtaxUsd(compra: 5.20m, venda: 5.22m);
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        fxRepo.GetMaisRecenteAsync(
                  Arg.Any<Moeda>(), Arg.Any<TipoCotacao>(),
                  Arg.Any<LocalDate>(), Arg.Any<CancellationToken>())
              .Returns(ptax);

        GetSaldoPorBancoAtualQueryHandler handler = CriarHandler(repo, bancoRepo, spotCache, fxRepo);

        // Act
        SaldoPorBancoAtualDto resultado = await handler.Handle(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Assert — midrate = (5,20 + 5,22) / 2 = 5,21 → 1_000_000 × 5,21 = 5_210_000
        resultado.Bancos.Should().HaveCount(1);
        resultado.Bancos[0].BancoId.Should().Be(banco.Id);
        resultado.Bancos[0].SaldoBrl.Should().Be(5_210_000m);
        resultado.SaldoTotalBrl.Should().Be(5_210_000m);
    }

    // ── Teste 4: múltiplos bancos — agrupa saldo por banco ─────────────────────

    [Fact]
    public async Task Handle_multiplosBancos_agrupaSaldoPorBanco()
    {
        // Arrange
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        Banco bancoA = CriarBancoNoRepo(bancoRepo, "003", "BancoA");
        Banco bancoB = CriarBancoNoRepo(bancoRepo, "004", "BancoB");

        Contrato c1 = CriarContratoBrl(bancoA.Id, 1_000_000m);
        Contrato c2 = CriarContratoBrl(bancoA.Id, 500_000m);   // segundo contrato no mesmo banco
        Contrato c3 = CriarContratoBrl(bancoB.Id, 2_000_000m);

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        repo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { c1, c2, c3 }.AsReadOnly());

        GetSaldoPorBancoAtualQueryHandler handler = CriarHandler(repo, bancoRepo);

        // Act
        SaldoPorBancoAtualDto resultado = await handler.Handle(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Assert — dois bancos distintos
        resultado.Bancos.Should().HaveCount(2);

        SaldoBancoAtualDto saldoA = resultado.Bancos.Single(b => b.BancoId == bancoA.Id);
        saldoA.SaldoBrl.Should().Be(1_500_000m);
        saldoA.QuantidadeContratosAtivos.Should().Be(2);

        SaldoBancoAtualDto saldoB = resultado.Bancos.Single(b => b.BancoId == bancoB.Id);
        saldoB.SaldoBrl.Should().Be(2_000_000m);
        saldoB.QuantidadeContratosAtivos.Should().Be(1);

        resultado.SaldoTotalBrl.Should().Be(3_500_000m);
    }

    // ── Teste 5: contrato Liquidado não entra no saldo ─────────────────────────

    [Fact]
    public async Task Handle_contratoEncerrado_naoEntraNoSaldo()
    {
        // Arrange
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        Banco banco = CriarBancoNoRepo(bancoRepo, "005", "BancoE");

        Contrato ativo = CriarContratoBrl(banco.Id, 1_000_000m);

        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(InstanteFixo);
        Contrato liquidado = CriarContratoBrl(banco.Id, 999_000m);
        liquidado.Liquidar(clock); // Status = Liquidado (não Ativo)

        // O repositório devolve ambos; o handler filtra por Status == Ativo
        IContratoRepository repo = Substitute.For<IContratoRepository>();
        repo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { ativo, liquidado }.AsReadOnly());

        GetSaldoPorBancoAtualQueryHandler handler = CriarHandler(repo, bancoRepo);

        // Act
        SaldoPorBancoAtualDto resultado = await handler.Handle(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Assert — apenas o contrato ativo contribui
        resultado.SaldoTotalBrl.Should().Be(1_000_000m);
        resultado.Bancos.Should().HaveCount(1);
        resultado.Bancos[0].QuantidadeContratosAtivos.Should().Be(1);
    }

    // ── Teste 6: soma total igual à soma dos saldos por banco ──────────────────

    [Fact]
    public async Task Handle_somaTotal_igualSomaDosSaldosPorBanco()
    {
        // Arrange
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        Banco bancoA = CriarBancoNoRepo(bancoRepo, "006", "BancoSoma1");
        Banco bancoB = CriarBancoNoRepo(bancoRepo, "007", "BancoSoma2");
        Banco bancoC = CriarBancoNoRepo(bancoRepo, "008", "BancoSoma3");

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        repo.ListAsync(default).ReturnsForAnyArgs(new List<Contrato>
        {
            CriarContratoBrl(bancoA.Id, 1_111_111.11m),
            CriarContratoBrl(bancoB.Id, 2_222_222.22m),
            CriarContratoBrl(bancoC.Id, 3_333_333.33m),
        }.AsReadOnly());

        GetSaldoPorBancoAtualQueryHandler handler = CriarHandler(repo, bancoRepo);

        // Act
        SaldoPorBancoAtualDto resultado = await handler.Handle(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Assert — invariante: total == soma dos saldos individuais
        decimal somaCalculada = resultado.Bancos.Sum(b => b.SaldoBrl);
        resultado.SaldoTotalBrl.Should().Be(somaCalculada,
            "SaldoTotalBrl deve ser exatamente a soma de SaldoBrl de cada banco");
    }

    // ── Teste 7: contrato USD com spot intraday disponível usa spot ────────────

    [Fact]
    public async Task Handle_contratoUsd_comSpotDisponivel_usaSpotIntraday()
    {
        // Arrange
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();
        Banco banco = CriarBancoNoRepo(bancoRepo, "009", "BancoSpot");

        Contrato contrato = CriarContratoUsd(banco.Id, 1_000_000m);

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        repo.ListAsync(default).ReturnsForAnyArgs(
            new List<Contrato> { contrato }.AsReadOnly());

        // Spot intraday disponível: taxa 5,50
        ICotacaoSpotCache spotCache = Substitute.For<ICotacaoSpotCache>();
        spotCache.GetSpotAsync(Arg.Any<Moeda>(), Arg.Any<CancellationToken>())
                 .Returns(new Money(5.50m, Moeda.Brl));

        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();

        GetSaldoPorBancoAtualQueryHandler handler = CriarHandler(repo, bancoRepo, spotCache, fxRepo);

        // Act
        SaldoPorBancoAtualDto resultado = await handler.Handle(
            new GetSaldoPorBancoAtualQuery(),
            CancellationToken.None);

        // Assert — 1_000_000 × 5,50 = 5_500_000
        resultado.Bancos[0].SaldoBrl.Should().Be(5_500_000m);

        // PTAX não deve ter sido consultada quando spot disponível
        await fxRepo.DidNotReceiveWithAnyArgs()
                    .GetMaisRecenteAsync(default, default, default, default);
    }
}

using FluentAssertions;

using NodaTime;

using NSubstitute;

using Sgcf.Application.Bancos;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Commands;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

using Xunit;

namespace Sgcf.Application.Tests.Contratos;

/// <summary>
/// Testes unitários do handler de criação de contratos REFINIMP usando mocks (NSubstitute).
/// Verifica as regras de negócio sem depender de banco de dados.
/// </summary>
[Trait("Category", "Domain")]
public sealed class CreateRefinimpCommandTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 6, 1, 9, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static Banco CriarBancoComRefinimp(string codigoCompe)
    {
        IClock clock = CriarClock();
        Banco banco = Banco.Criar(codigoCompe, "Banco Teste S.A.", "Teste", PadraoAntecipacao.A, clock);
        banco.AtualizarAceitaRefinimp(aceitaRefinimp: true, clock);
        return banco;
    }

    private static Contrato CriarContratoFinimp(Guid bancoId, decimal valorPrincipal = 1_000_000m)
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 1, 15, 10, 0));

        return Contrato.Criar(
            numeroExterno: "FIN-2026-001",
            bancoId: bancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(valorPrincipal, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 1, 15),
            dataVencimento: new LocalDate(2027, 1, 15),
            taxaAa: Percentual.De(6m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    private static CreateContratoCommand CriarComandoRefinimp(
        Guid bancoId,
        Guid contratoPaiId,
        Guid contratoMaeId,
        string moeda = "Usd",
        decimal valorPrincipal = 700_000m)
    {
        return new CreateContratoCommand(
            NumeroExterno: "REF-2026-001",
            BancoId: bancoId,
            Modalidade: "Refinimp",
            Moeda: moeda,
            ValorPrincipal: valorPrincipal,
            DataContratacao: new DateOnly(2026, 6, 1),
            DataVencimento: new DateOnly(2027, 6, 1),
            TaxaAa: 5m,
            BaseCalculo: "Dias360",
            ContratoPaiId: contratoPaiId,
            Observacoes: null,
            FinimpDetail: null,
            Lei4131Detail: null,
            RefinimpDetail: new RefinimpDetailRequest(contratoMaeId, 70m));
    }

    // ── Teste 1: BB rejeita REFINIMP > 70% do principal ancestral ──────────────

    [Fact]
    public async Task Handle_BancoBB_ValorAcima70PorCento_LancaInvalidOperationException()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Guid contratoPaiId = Guid.NewGuid();

        // Banco do Brasil — CodigoCompe "001"
        Banco bancoBB = CriarBancoComRefinimp("001");

        // Parent principal = 1_000_000 → 70% limit = 700_000
        Contrato contratoPai = CriarContratoFinimp(bancoId, valorPrincipal: 1_000_000m);

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();

        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(bancoBB);
        repo.GetByIdAsync(contratoPaiId, Arg.Any<CancellationToken>()).Returns(contratoPai);
        repo.GetAncestraNaoRefinimpAsync(contratoPaiId, Arg.Any<CancellationToken>()).Returns(contratoPai);
        repo.CountByAnoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);

        CreateContratoCommandHandler handler = new(repo, CriarClock(), bancoRepo);

        // 710_000 > 70% of 1_000_000 (= 700_000) → must reject
        CreateContratoCommand cmd = CriarComandoRefinimp(bancoId, contratoPaiId, contratoPaiId, valorPrincipal: 710_000m);

        // Act
        Func<Task> act = () => handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*70%*");
    }

    // ── Teste 2: Sucesso — pai marcado como RefinanciadoParcial quando < 100% ──

    [Fact]
    public async Task Handle_ValorAbaixo100PorCento_MarcaPaiComoRefinanciadoParcial()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Guid contratoPaiId = Guid.NewGuid();

        Banco banco = CriarBancoComRefinimp("237"); // Bradesco — sem restrição BB

        // Parent principal = 1_000_000; REFINIMP value = 600_000 → 60% → partial
        Contrato contratoPai = CriarContratoFinimp(bancoId, valorPrincipal: 1_000_000m);

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();

        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);
        repo.GetByIdAsync(contratoPaiId, Arg.Any<CancellationToken>()).Returns(contratoPai);
        repo.GetAncestraNaoRefinimpAsync(contratoPaiId, Arg.Any<CancellationToken>()).Returns(contratoPai);
        repo.CountByAnoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);
        repo.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        CreateContratoCommandHandler handler = new(repo, CriarClock(), bancoRepo);

        CreateContratoCommand cmd = CriarComandoRefinimp(bancoId, contratoPaiId, contratoPaiId, valorPrincipal: 600_000m);

        // Act
        ContratoDto result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        contratoPai.Status.Should().Be(StatusContrato.RefinanciadoParcial);

        repo.Received(1).AddRefinimpDetail(Arg.Any<RefinimpDetail>());
        await repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Teste 3: Moeda do REFINIMP diferente da do contrato mãe → lança ────────

    [Fact]
    public async Task Handle_MoedaDivergente_LancaInvalidOperationException()
    {
        // Arrange
        Guid bancoId = Guid.NewGuid();
        Guid contratoPaiId = Guid.NewGuid();

        Banco banco = CriarBancoComRefinimp("237");

        // Parent contract is in USD
        Contrato contratoPai = CriarContratoFinimp(bancoId, valorPrincipal: 1_000_000m);

        IContratoRepository repo = Substitute.For<IContratoRepository>();
        IBancoRepository bancoRepo = Substitute.For<IBancoRepository>();

        bancoRepo.GetByIdAsync(bancoId, Arg.Any<CancellationToken>()).Returns(banco);
        repo.GetByIdAsync(contratoPaiId, Arg.Any<CancellationToken>()).Returns(contratoPai);
        repo.CountByAnoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);

        CreateContratoCommandHandler handler = new(repo, CriarClock(), bancoRepo);

        // REFINIMP in BRL — mismatched with parent's USD
        CreateContratoCommand cmd = CriarComandoRefinimp(
            bancoId, contratoPaiId, contratoPaiId,
            moeda: "Brl",          // BRL != USD (parent)
            valorPrincipal: 500_000m);

        // Act
        Func<Task> act = () => handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*moeda*");
    }
}

using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Contratos;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes de integração leve para <see cref="CriarCotacaoCommandHandler"/> com modalidade REFINIMP.
/// SPEC §4.1 — Onda 1.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CriarCotacaoRefinimpTests
{
    private static readonly Instant Agora = Instant.FromUtc(2026, 6, 1, 12, 0);
    private static readonly LocalDate DataAbertura = new(2026, 6, 1);
    private static readonly LocalDate DataPtax = new(2026, 5, 31);
    private const decimal PtaxValida = 5.20m;

    private static readonly Guid ContratoMaeValido = Guid.NewGuid();

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return clock;
    }

    private static CotacaoFx CriarPtaxFx() =>
        CotacaoFx.Criar(
            Moeda.Usd, TipoCotacao.PtaxD1,
            new Money(PtaxValida - 0.05m, Moeda.Brl),
            new Money(PtaxValida, Moeda.Brl),
            "BACEN",
            Agora.Minus(Duration.FromHours(13))); // 2026-05-31T23:00Z

    private static Contrato CriarContratoMaeAtivo()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Agora);
        return Contrato.Criar(
            numeroExterno: "FIN-2026-0001",
            bancoId: Guid.NewGuid(),
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(1_000_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2025, 12, 1),
            dataVencimento: new LocalDate(2026, 12, 1),
            taxaAa: Percentual.De(6.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);
    }

    // ── Cenários de sucesso ─────────────────────────────────────────────────

    [Fact(DisplayName = "Handle: REFINIMP com mãe Ativo deve criar cotação e retornar 201")]
    public async Task Handle_Refinimp_mae_Ativo_sucesso()
    {
        // Arrange
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarPtaxFx());
        repo.GerarProximoCodigoInternoAsync(DataAbertura.Year, default)
            .Returns("COT-2026-R0001");

        Contrato mae = CriarContratoMaeAtivo();
        contratoRepo.GetByIdAsync(ContratoMaeValido, default).Returns(mae);

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock, contratoRepo);

        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Refinimp",
            ValorAlvoBrl: 500_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 6, 1),
            ContratoMaeId: ContratoMaeValido);

        // Act
        CotacaoDto resultado = await handler.Handle(cmd, default);

        // Assert
        resultado.Modalidade.Should().Be("Refinimp");
        resultado.ContratoMaeId.Should().Be(ContratoMaeValido);
        repo.Received(1).Add(Arg.Any<Cotacao>());
    }

    [Fact(DisplayName = "Handle: REFINIMP com mãe RefinanciadoParcial deve ter sucesso (cadeia recursiva)")]
    public async Task Handle_Refinimp_mae_RefinanciadoParcial_sucesso()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarPtaxFx());
        repo.GerarProximoCodigoInternoAsync(DataAbertura.Year, default)
            .Returns("COT-2026-R0002");

        Contrato mae = CriarContratoMaeAtivo();
        mae.MarcarRefinanciadoParcial(clock);
        contratoRepo.GetByIdAsync(ContratoMaeValido, default).Returns(mae);

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock, contratoRepo);

        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Refinimp",
            ValorAlvoBrl: 300_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 6, 1),
            ContratoMaeId: ContratoMaeValido);

        // Act — não deve lançar
        CotacaoDto resultado = await handler.Handle(cmd, default);

        resultado.Modalidade.Should().Be("Refinimp");
    }

    // ── Cenários de erro ────────────────────────────────────────────────────

    [Fact(DisplayName = "Handle: REFINIMP com mãe inexistente lança KeyNotFoundException")]
    public async Task Handle_Refinimp_mae_inexistente_lanca_KeyNotFound()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarPtaxFx());
        repo.GerarProximoCodigoInternoAsync(DataAbertura.Year, default)
            .Returns("COT-2026-R0001");

        contratoRepo.GetByIdAsync(ContratoMaeValido, default).Returns((Contrato?)null);

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock, contratoRepo);

        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Refinimp",
            ValorAlvoBrl: 500_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 6, 1),
            ContratoMaeId: ContratoMaeValido);

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{ContratoMaeValido}*");
    }

    [Fact(DisplayName = "Handle: REFINIMP com mãe Cancelado lança InvalidOperationException")]
    public async Task Handle_Refinimp_mae_Cancelado_lanca_InvalidOperation_409()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarPtaxFx());
        repo.GerarProximoCodigoInternoAsync(DataAbertura.Year, default)
            .Returns("COT-2026-R0001");

        // Força Status = Cancelado via reflexão (Contrato não expõe Cancelar() publicamente)
        Contrato mae = CriarContratoMaeAtivo();
        typeof(Contrato).GetProperty("Status")!
            .SetValue(mae, StatusContrato.Cancelado);
        contratoRepo.GetByIdAsync(ContratoMaeValido, default).Returns(mae);

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock, contratoRepo);

        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Refinimp",
            ValorAlvoBrl: 500_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 6, 1),
            ContratoMaeId: ContratoMaeValido);

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Cancelado*");
    }

    [Fact(DisplayName = "Handle: REFINIMP com mãe Liquidado (quitado) lança InvalidOperationException")]
    public async Task Handle_Refinimp_mae_Liquidado_lanca_InvalidOperation_409()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarPtaxFx());
        repo.GerarProximoCodigoInternoAsync(DataAbertura.Year, default)
            .Returns("COT-2026-R0001");

        Contrato mae = CriarContratoMaeAtivo();
        mae.Liquidar(clock);  // StatusContrato.Liquidado = "quitado" no negócio
        contratoRepo.GetByIdAsync(ContratoMaeValido, default).Returns(mae);

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock, contratoRepo);

        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Refinimp",
            ValorAlvoBrl: 500_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 6, 1),
            ContratoMaeId: ContratoMaeValido);

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*Liquidado*");
    }

    [Fact(DisplayName = "Handle: REFINIMP com mãe RefinanciadoTotal lança InvalidOperationException")]
    public async Task Handle_Refinimp_mae_RefinanciadoTotal_lanca_InvalidOperation_409()
    {
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IContratoRepository contratoRepo = Substitute.For<IContratoRepository>();
        IClock clock = CriarClock();

        fxRepo.GetMaisRecenteAsync(Moeda.Usd, TipoCotacao.PtaxD1, DataPtax, default)
            .Returns(CriarPtaxFx());
        repo.GerarProximoCodigoInternoAsync(DataAbertura.Year, default)
            .Returns("COT-2026-R0001");

        Contrato mae = CriarContratoMaeAtivo();
        mae.MarcarRefinanciadoTotal(clock);
        contratoRepo.GetByIdAsync(ContratoMaeValido, default).Returns(mae);

        CriarCotacaoCommandHandler handler = new(repo, fxRepo, clock, contratoRepo);

        CriarCotacaoCommand cmd = new(
            CodigoInterno: null,
            Modalidade: "Refinimp",
            ValorAlvoBrl: 500_000m,
            PrazoMaximoDias: 180,
            DataAbertura: new DateOnly(2026, 6, 1),
            ContratoMaeId: ContratoMaeValido);

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*RefinanciadoTotal*");
    }
}

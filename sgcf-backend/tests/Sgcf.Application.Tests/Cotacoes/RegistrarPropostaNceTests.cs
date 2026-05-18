using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes de unidade para validações NCE em <see cref="RegistrarPropostaCommandHandler"/>.
/// SPEC §4.2 e §8 (edge cases EC-2 e EC-3) — Onda 2.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RegistrarPropostaNceTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 16, 9, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 16);

    /// <summary>
    /// Cria cotação NCE em status EmCaptacao (sem PTAX — operação BRL pura).
    /// </summary>
    private static Cotacao CriarCotacaoNceEmCaptacao()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-NCE-001",
            modalidade: ModalidadeContrato.Nce,
            valorAlvoBrl: new Money(1_500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: clock);

        cotacao.Enviar(clock);
        return cotacao;
    }

    private static RegistrarPropostaCommand CriarComandoNceBrl(
        Guid cotacaoId,
        Guid bancoId,
        string moedaOriginal = "Brl",
        bool exigeNdf = false,
        decimal? custoNdfAa = null,
        string periodicidade = "Trimestral") =>
        new(cotacaoId, bancoId,
            MoedaOriginal: moedaOriginal,
            ValorOferecido: 1_500_000m,
            TaxaAa: 14.5m,
            IofPct: 0.38m,
            SpreadAa: 0m,
            PrazoDias: 180,
            EstruturaAmortizacao: "Bullet",
            PeriodicidadeJuros: periodicidade,
            ExigeNdf: exigeNdf,
            CustoNdfAa: custoNdfAa,
            GarantiaExigida: "Aval dos sócios + duplicatas de exportação",
            ValorGarantiaBrl: 0m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

    // ── EC-3: Proposta NCE com moeda diferente de BRL → 400 ───────────────────

    /// <summary>
    /// EC-3: proposta NCE com MoedaOriginal=Usd deve ser rejeitada.
    /// SPEC §8 e §5.2: "Proposta NCE deve ser em BRL — modalidade não suporta conversão cambial."
    /// </summary>
    [Fact]
    public async Task Handle_NCE_com_moeda_USD_lanca_ArgumentException()
    {
        Cotacao cotacao = CriarCotacaoNceEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoNceBrl(
            cotacaoId: cotacao.Id,
            bancoId: Guid.NewGuid(),
            moedaOriginal: "Usd");

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NCE*BRL*");
    }

    /// <summary>
    /// EC-2: proposta NCE com ExigeNdf=true deve ser rejeitada.
    /// SPEC §8: "Proposta NCE não aceita NDF — operação em BRL sem exposição cambial."
    /// </summary>
    [Fact]
    public async Task Handle_NCE_com_ExigeNdf_true_lanca_ArgumentException()
    {
        Cotacao cotacao = CriarCotacaoNceEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoNceBrl(
            cotacaoId: cotacao.Id,
            bancoId: Guid.NewGuid(),
            exigeNdf: true,
            custoNdfAa: 1.5m);

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NCE*NDF*");
    }

    /// <summary>
    /// Caminho positivo: proposta NCE BRL sem NDF deve ser registrada com CET calculado.
    /// </summary>
    [Fact]
    public async Task Handle_NCE_BRL_sem_NDF_sucesso_com_CET_calculado()
    {
        Cotacao cotacao = CriarCotacaoNceEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoNceBrl(
            cotacaoId: cotacao.Id,
            bancoId: Guid.NewGuid());

        PropostaDto resultado = await handler.Handle(cmd, default);

        resultado.MoedaOriginal.Should().Be("Brl");
        resultado.CetCalculadoAaPercentual.Should().NotBeNull();
        resultado.CetCalculadoAaPercentual.Should().BeGreaterThan(0m,
            because: "NCE BRL com IOF 0,38% deve ter CET positivo");
        await repo.Received(1).SaveChangesAsync(default);
    }

    /// <summary>
    /// Todas as periodicidades aceitas pela NCE devem ser processadas sem erro.
    /// SPEC §3.2: Bullet, Mensal, Bimestral, Trimestral, Semestral, Anual.
    /// </summary>
    [Theory]
    [InlineData("Bullet", 360)]
    [InlineData("Mensal", 180)]
    [InlineData("Trimestral", 360)]
    [InlineData("Semestral", 360)]
    [InlineData("Anual", 360)]
    public async Task Handle_NCE_todas_periodicidades_validas_registram_com_sucesso(
        string periodicidade, int prazoDias)
    {
        Cotacao cotacao = CriarCotacaoNceEmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = new(
            cotacao.Id, Guid.NewGuid(),
            MoedaOriginal: "Brl",
            ValorOferecido: 1_500_000m,
            TaxaAa: 14.5m,
            IofPct: 0.38m,
            SpreadAa: 0m,
            PrazoDias: prazoDias,
            EstruturaAmortizacao: "Bullet",
            PeriodicidadeJuros: periodicidade,
            ExigeNdf: false,
            CustoNdfAa: null,
            GarantiaExigida: "Aval",
            ValorGarantiaBrl: 0m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

        PropostaDto resultado = await handler.Handle(cmd, default);

        resultado.Should().NotBeNull();
        resultado.CetCalculadoAaPercentual.Should().BeGreaterThan(0m);
    }
}

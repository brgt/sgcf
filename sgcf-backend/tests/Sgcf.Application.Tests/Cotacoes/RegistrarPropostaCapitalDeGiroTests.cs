using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cambio;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes;

/// <summary>
/// Testes de unidade para validações Capital de Giro em <see cref="RegistrarPropostaCommandHandler"/>.
/// SPEC §4.2 e §8 (edge cases EC-2, EC-3) — Onda 3b.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RegistrarPropostaCapitalDeGiroTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 18, 9, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 18);

    private static Cotacao CriarCotacaoCapitalDeGiroEmCaptacao()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-CDG-TEST",
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorAlvoBrl: new Money(1_500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: clock);

        cotacao.Enviar(clock);
        return cotacao;
    }

    private static RegistrarPropostaCommand CriarComandoCdgBrl(
        Guid cotacaoId,
        Guid bancoId,
        string moedaOriginal = "Brl",
        bool exigeNdf = false,
        decimal? custoNdfAa = null,
        string periodicidade = "Mensal") =>
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
            GarantiaExigida: "Aval dos sócios",
            ValorGarantiaBrl: 0m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

    // ── EC-3: moeda diferente de BRL ─────────────────────────────────────────

    /// <summary>
    /// SPEC §4.2 EC-3: proposta Capital de Giro com moeda USD deve ser rejeitada.
    /// </summary>
    [Fact]
    public async Task Handle_rejeita_proposta_cdg_com_moeda_usd()
    {
        Cotacao cotacao = CriarCotacaoCapitalDeGiroEmCaptacao();

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.GetByIdWithPropostasAsync(cotacao.Id, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        var handler = new RegistrarPropostaCommandHandler(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoCdgBrl(
            cotacao.Id, Guid.NewGuid(), moedaOriginal: "Usd");

        Func<Task> act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*BRL*");
    }

    // ── EC-2: NDF em Capital de Giro ─────────────────────────────────────────

    /// <summary>
    /// SPEC §4.2 EC-2: proposta Capital de Giro com ExigeNdf=true deve ser rejeitada.
    /// Capital de Giro é operação BRL sem exposição cambial.
    /// </summary>
    [Fact]
    public async Task Handle_rejeita_proposta_cdg_com_ExigeNdf_true()
    {
        Cotacao cotacao = CriarCotacaoCapitalDeGiroEmCaptacao();

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.GetByIdWithPropostasAsync(cotacao.Id, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        var handler = new RegistrarPropostaCommandHandler(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoCdgBrl(
            cotacao.Id, Guid.NewGuid(), exigeNdf: true, custoNdfAa: 1.5m);

        Func<Task> act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NDF*");
    }

    // ── Caminho feliz: proposta BRL válida ───────────────────────────────────

    /// <summary>
    /// Proposta Capital de Giro BRL sem NDF deve ser aceita e retornar PropostaDto.
    /// </summary>
    [Fact]
    public async Task Handle_aceita_proposta_cdg_brl_valida()
    {
        Cotacao cotacao = CriarCotacaoCapitalDeGiroEmCaptacao();

        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        repo.GetByIdWithPropostasAsync(cotacao.Id, Arg.Any<CancellationToken>())
            .Returns(cotacao);

        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);

        var handler = new RegistrarPropostaCommandHandler(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoCdgBrl(cotacao.Id, Guid.NewGuid());

        PropostaDto result = await handler.Handle(cmd, CancellationToken.None);

        result.Should().NotBeNull();
        result.MoedaOriginal.Should().Be("Brl");
        result.CetCalculadoAaPercentual.Should().BeGreaterThan(0m,
            because: "CET deve ser calculado automaticamente pelo handler");
    }
}

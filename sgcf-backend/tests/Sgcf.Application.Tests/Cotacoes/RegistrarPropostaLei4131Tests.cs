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
/// Testes de unidade para validações Lei 4131 em <see cref="RegistrarPropostaCommandHandler"/>.
/// SPEC §4.2, §5.2 — Onda 4.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RegistrarPropostaLei4131Tests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 18, 9, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 18);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    /// <summary>
    /// Cria cotação Lei 4131 em EmCaptacao (com PTAX — operação cambial).
    /// </summary>
    private static Cotacao CriarCotacaoLei4131EmCaptacao()
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-L4131-T01",
            modalidade: ModalidadeContrato.Lei4131,
            valorAlvoBrl: new Money(25_000_000m, Moeda.Brl),
            prazoMaximoDias: 720,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: new LocalDate(2026, 5, 17),
            ptaxUsadaUsdBrl: 5.0123m,
            clock: clock);

        cotacao.Enviar(clock);
        return cotacao;
    }

    private static RegistrarPropostaCommand CriarComandoLei4131(
        Guid cotacaoId,
        Guid bancoId,
        string moedaOriginal = "Usd",
        bool exigeNdf = false,
        decimal? custoNdfAa = null) =>
        new(cotacaoId, bancoId,
            MoedaOriginal: moedaOriginal,
            ValorOferecido: 5_000_000m,
            TaxaAa: 6.25m,
            IofPct: 0.38m,
            SpreadAa: 0.50m,
            PrazoDias: 720,
            EstruturaAmortizacao: "Bullet",
            PeriodicidadeJuros: "Bullet",
            ExigeNdf: exigeNdf,
            CustoNdfAa: custoNdfAa,
            GarantiaExigida: "SBLC 100% (obrigatório)",
            ValorGarantiaBrl: 25_000_000m,
            GarantiaEhCdbCativo: false,
            RendimentoCdbAa: null);

    // ── Lei 4131 com BRL deve ser rejeitada ───────────────────────────────────

    /// <summary>
    /// EC-Lei4131-1: proposta Lei 4131 com MoedaOriginal=Brl deve ser rejeitada.
    /// SPEC §4.2 e §5.2: "Lei 4131 obriga moeda estrangeira. Proposta em BRL é inválida."
    /// </summary>
    [Fact]
    public async Task Handle_Lei4131_com_moeda_BRL_lanca_ArgumentException()
    {
        Cotacao cotacao = CriarCotacaoLei4131EmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoLei4131(
            cotacaoId: cotacao.Id,
            bancoId: Guid.NewGuid(),
            moedaOriginal: "Brl");

        Func<Task> act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Lei*4131*BRL*");
    }

    /// <summary>
    /// Lei 4131 com moeda USD (válida) não deve lançar exceção de moeda.
    /// Verifica que a guard só rejeita BRL, não moedas estrangeiras.
    /// </summary>
    [Fact]
    public async Task Handle_Lei4131_com_moeda_USD_nao_lanca_excecao_de_moeda()
    {
        Cotacao cotacao = CriarCotacaoLei4131EmCaptacao();
        ICotacaoRepository repo = Substitute.For<ICotacaoRepository>();
        ICotacaoFxRepository fxRepo = Substitute.For<ICotacaoFxRepository>();
        IClock clock = CriarClock();

        repo.GetByIdWithPropostasAsync(cotacao.Id, default).Returns(cotacao);

        RegistrarPropostaCommandHandler handler = new(repo, fxRepo, clock);
        RegistrarPropostaCommand cmd = CriarComandoLei4131(
            cotacaoId: cotacao.Id,
            bancoId: Guid.NewGuid(),
            moedaOriginal: "Usd");

        // Deve passar pela guard de moeda sem lançar ArgumentException.
        // (Pode lançar outros erros downstream, mas não ArgumentException por moeda.)
        Exception? ex = null;
        try
        {
            await handler.Handle(cmd, default);
        }
        catch (ArgumentException argEx) when (argEx.Message.Contains("BRL"))
        {
            ex = argEx;
        }
        catch
        {
            // Outros erros são aceitáveis — a guard de moeda não deve ter sido acionada.
        }

        ex.Should().BeNull("proposta USD não deve ser rejeitada pela guard de moeda Lei 4131");
    }
}

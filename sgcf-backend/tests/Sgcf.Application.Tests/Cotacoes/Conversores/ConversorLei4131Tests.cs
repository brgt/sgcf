using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Application.Cotacoes;
using Sgcf.Application.Cotacoes.Commands;
using Sgcf.Application.Cotacoes.Conversores;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Application.Tests.Cotacoes.Conversores;

/// <summary>
/// Testes unitários do ConversorLei4131 (implementação real — Onda 4).
/// SPEC §6.1 — substitui o stub NotImplementedException da Onda 0.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversorLei4131Tests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 18, 12, 0);
    private static readonly LocalDate DataAbertura = new(2026, 5, 18);
    private static readonly LocalDate DataPtaxRef = new(2026, 5, 17);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    /// <summary>
    /// Cria contexto completo para conversão Lei 4131.
    /// </summary>
    private static ConverterEmContratoContext CriarContexto(
        IClock clock,
        Lei4131Inputs? lei4131Inputs = null)
    {
        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-L4131-01",
            modalidade: ModalidadeContrato.Lei4131,
            valorAlvoBrl: new Money(25_000_000m, Moeda.Brl),
            prazoMaximoDias: 720,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtaxRef,
            ptaxUsadaUsdBrl: 5.0123m,
            clock: clock);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: Guid.NewGuid(),
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(5_000_000m, Moeda.Usd),
            taxaAaPercentual: 6.25m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0.50m,
            prazoDias: 720,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "SBLC 100% (obrigatório)",
            valorGarantiaExigidaBrl: new Money(25_000_000m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataAbertura);

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        Contrato contrato = Contrato.Criar(
            numeroExterno: "LEI4131-2026-00042",
            bancoId: proposta.BancoId,
            modalidade: ModalidadeContrato.Lei4131,
            valorPrincipal: proposta.ValorOferecidoMoedaOriginal,
            dataContratacao: new LocalDate(2026, 5, 20),
            dataVencimento: new LocalDate(2028, 5, 18),
            taxaAa: Percentual.De(6.25m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        ConverterEmContratoCommand command = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "LEI4131-2026-00042",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 5, 20),
            DataVencimento: new DateOnly(2028, 5, 18),
            TaxaAa: 6.25m,
            Lei4131: lei4131Inputs);

        return new ConverterEmContratoContext(cotacao, proposta, contrato, command, clock);
    }

    // ── Modalidade ────────────────────────────────────────────────────────────

    [Fact]
    public void ConversorLei4131_retorna_modalidade_Lei4131()
    {
        new ConversorLei4131().Modalidade.Should().Be(ModalidadeContrato.Lei4131);
    }

    // ── CriarDetail com SBLC completo ─────────────────────────────────────────

    /// <summary>
    /// Cenário principal SPEC §6.1: SBLC completo com market flex e break funding.
    /// O conversor deve criar Lei4131Detail com os campos persistíveis preenchidos.
    /// </summary>
    [Fact]
    public async Task CriarDetail_SblcCompleto_CriaLei4131DetailComDadosPersistidos()
    {
        IClock clock = CriarClock();
        Lei4131Inputs inputs = new(
            SblcNumero: "SBLC-2026-001234",
            SblcBancoEmissor: "Itaú Unibanco S.A.",
            SblcValorUsd: 5_000_000m,
            TemMarketFlex: false,
            BreakFundingFeePercentual: 1.5m,
            PaisCredor: "USA",
            AliquotaIrrfPercentual: 15m);

        ConverterEmContratoContext ctx = CriarContexto(clock, inputs);
        ConversorLei4131 conversor = new();

        (Entity detail, Entity? secundario) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        detail.Should().BeOfType<Lei4131Detail>();
        secundario.Should().BeNull();

        Lei4131Detail lei4131 = (Lei4131Detail)detail;
        lei4131.SblcNumero.Should().Be("SBLC-2026-001234");
        lei4131.SblcBancoEmissor.Should().Be("Itaú Unibanco S.A.");
        lei4131.SblcValorUsd.Should().Be(5_000_000m);
        lei4131.TemMarketFlex.Should().BeFalse();
        // BreakFundingFee é armazenado como fração (divide por 100): 1.5% → 0.015
        lei4131.BreakFundingFeePercentual.Should().BeApproximately(0.015m, 0.00001m);
    }

    /// <summary>
    /// Operação "clean Lei 4131" (sem SBLC) — SPEC §2.3.
    /// Todos os campos SBLC são null; conversor deve aceitar sem erro.
    /// </summary>
    [Fact]
    public async Task CriarDetail_SemSblc_CriaLei4131DetailComCamposSblcNull()
    {
        IClock clock = CriarClock();
        Lei4131Inputs inputs = new(
            SblcNumero: null,
            SblcBancoEmissor: null,
            SblcValorUsd: null,
            TemMarketFlex: false,
            BreakFundingFeePercentual: null,
            PaisCredor: "JPN",
            AliquotaIrrfPercentual: 12.5m);

        ConverterEmContratoContext ctx = CriarContexto(clock, inputs);
        ConversorLei4131 conversor = new();

        (Entity detail, Entity? secundario) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        detail.Should().BeOfType<Lei4131Detail>();
        secundario.Should().BeNull();

        Lei4131Detail lei4131 = (Lei4131Detail)detail;
        lei4131.SblcNumero.Should().BeNull();
        lei4131.SblcBancoEmissor.Should().BeNull();
        lei4131.SblcValorUsd.Should().BeNull();
        lei4131.BreakFundingFeePercentual.Should().BeNull();
    }

    /// <summary>
    /// ContratoId do detail deve corresponder ao contrato criado no contexto.
    /// Garante integridade referencial antes do SaveChanges.
    /// </summary>
    [Fact]
    public async Task CriarDetail_ContratoId_CorrespondeAoContratoCriado()
    {
        IClock clock = CriarClock();
        Lei4131Inputs inputs = new(null, null, null, false, null, null, null);
        ConverterEmContratoContext ctx = CriarContexto(clock, inputs);
        ConversorLei4131 conversor = new();

        (Entity detail, _) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        Lei4131Detail lei4131 = (Lei4131Detail)detail;
        lei4131.ContratoId.Should().Be(ctx.ContratoCriado.Id);
    }

    /// <summary>
    /// Quando Lei4131 é null no command (esquecido pelo caller), o conversor
    /// deve lançar InvalidOperationException com mensagem orientativa. SPEC §6.1.
    /// </summary>
    [Fact]
    public async Task CriarDetail_Lei4131InputsNull_LancaInvalidOperationException()
    {
        IClock clock = CriarClock();
        ConverterEmContratoContext ctx = CriarContexto(clock, lei4131Inputs: null);
        ConversorLei4131 conversor = new();

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Lei4131*");
    }

    /// <summary>
    /// PaisCredor e AliquotaIrrfPercentual NÃO são persistidos (SPEC §3.3).
    /// O detail criado não expõe esses campos — são descartados pelo conversor.
    /// </summary>
    [Fact]
    public async Task CriarDetail_PaisCredorEAliquota_NaoSaoPersistidos()
    {
        IClock clock = CriarClock();
        Lei4131Inputs inputs = new(
            SblcNumero: "SBLC-TEST",
            SblcBancoEmissor: "Test Bank",
            SblcValorUsd: 1_000_000m,
            TemMarketFlex: true,
            BreakFundingFeePercentual: 0.5m,
            PaisCredor: "JPN",               // não persistido
            AliquotaIrrfPercentual: 12.5m);  // não persistido

        ConverterEmContratoContext ctx = CriarContexto(clock, inputs);
        ConversorLei4131 conversor = new();

        (Entity detail, _) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        Lei4131Detail lei4131 = (Lei4131Detail)detail;
        // Lei4131Detail não tem PaisCredor nem AliquotaIrrf — compilador já garante,
        // mas o teste documenta explicitamente a decisão MD-5/AD-3.
        lei4131.Should().NotBeNull(); // entidade criada com sucesso sem os campos informativos
        lei4131.TemMarketFlex.Should().BeTrue();
    }
}

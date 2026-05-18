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
/// Testes unitários dos stubs de modalidades ainda não implementadas.
/// Cada stub deve lançar NotImplementedException com mensagem clara referenciando
/// a onda de entrega — comportamento contratual, não acidental.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversorStubsTests
{
    private static readonly LocalDate DataBase = new(2026, 5, 16);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 5, 18, 9, 0));
        return clock;
    }

    /// <summary>
    /// Constrói um contexto mínimo válido para testar stubs.
    /// Os stubs lançam NotImplementedException imediatamente sem acessar os campos,
    /// portanto qualquer instância válida serve.
    /// Para modalidades BRL (Nce, BalcaoCaixa, Fgi), PTAX deve ser null.
    /// Para modalidades cambiais (Refinimp, Lei4131), PTAX deve estar presente.
    /// </summary>
    private static ConverterEmContratoContext CriarContexto(ModalidadeContrato modalidade)
    {
        IClock clock = CriarClock();
        bool exigeEstrangeira = Cotacao.ExigeMoedaEstrangeira(modalidade);

        // Onda 1: REFINIMP exige ContratoMaeId — passa um Guid fictício para testes de stub.
        Guid? contratoMaeId = modalidade == ModalidadeContrato.Refinimp ? Guid.NewGuid() : null;

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: $"COT-STUB-{(int)modalidade:D2}",
            modalidade: modalidade,
            valorAlvoBrl: new Money(1_000_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataBase,
            dataPtaxReferencia: exigeEstrangeira ? new LocalDate(2026, 5, 15) : null,
            ptaxUsadaUsdBrl: exigeEstrangeira ? 5.20m : null,
            clock: clock,
            contratoMaeId: contratoMaeId);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: Guid.NewGuid(),
            moedaOriginal: exigeEstrangeira ? Moeda.Usd : Moeda.Brl,
            valorOferecidoMoedaOriginal: exigeEstrangeira
                ? new Money(100_000m, Moeda.Usd)
                : new Money(500_000m, Moeda.Brl),
            taxaAaPercentual: 6.5m,
            iofPercentual: 0.38m,
            spreadAaPercentual: 0.5m,
            prazoDias: 180,
            estruturaAmortizacao: EstruturaAmortizacao.Bullet,
            periodicidadeJuros: Periodicidade.Bullet,
            exigeNdf: false,
            custoNdfAaPercentual: null,
            garantiaExigida: "Aval",
            valorGarantiaExigidaBrl: new Money(600_000m, Moeda.Brl),
            garantiaEhCdbCativo: false,
            rendimentoCdbAaPercentual: null,
            dataCaptura: DataBase);

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        Contrato contrato = Contrato.Criar(
            numeroExterno: $"STUB-{(int)modalidade:D2}-001",
            bancoId: proposta.BancoId,
            modalidade: modalidade,
            valorPrincipal: proposta.ValorOferecidoMoedaOriginal,
            dataContratacao: new LocalDate(2026, 5, 20),
            dataVencimento: new LocalDate(2026, 11, 16),
            taxaAa: Percentual.De(6.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        ConverterEmContratoCommand command = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: $"STUB-{(int)modalidade:D2}-001",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 5, 20),
            DataVencimento: new DateOnly(2026, 11, 16),
            TaxaAa: 6.5m);

        return new ConverterEmContratoContext(cotacao, proposta, contrato, command, clock);
    }

    // ─── ConversorRefinimp ────────────────────────────────────────────────────

    [Fact]
    public void ConversorRefinimp_retorna_modalidade_Refinimp()
    {
        new ConversorRefinimp().Modalidade.Should().Be(ModalidadeContrato.Refinimp);
    }

    [Fact]
    public async Task ConversorRefinimp_lanca_NotImplementedException_com_mensagem_referenciando_spec()
    {
        ConversorRefinimp conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(ModalidadeContrato.Refinimp);

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*Refinimp*");
    }

    // ─── ConversorLei4131 ─────────────────────────────────────────────────────

    [Fact]
    public void ConversorLei4131_retorna_modalidade_Lei4131()
    {
        new ConversorLei4131().Modalidade.Should().Be(ModalidadeContrato.Lei4131);
    }

    [Fact]
    public async Task ConversorLei4131_lanca_NotImplementedException()
    {
        ConversorLei4131 conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(ModalidadeContrato.Lei4131);

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*Lei4131*");
    }

    // ─── ConversorNce ─────────────────────────────────────────────────────────

    [Fact]
    public void ConversorNce_retorna_modalidade_Nce()
    {
        new ConversorNce().Modalidade.Should().Be(ModalidadeContrato.Nce);
    }

    [Fact]
    public async Task ConversorNce_lanca_NotImplementedException()
    {
        ConversorNce conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(ModalidadeContrato.Nce);

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*Nce*");
    }

    // ─── ConversorBalcaoCaixa ─────────────────────────────────────────────────

    [Fact]
    public void ConversorBalcaoCaixa_retorna_modalidade_BalcaoCaixa()
    {
        new ConversorBalcaoCaixa().Modalidade.Should().Be(ModalidadeContrato.BalcaoCaixa);
    }

    [Fact]
    public async Task ConversorBalcaoCaixa_lanca_NotImplementedException()
    {
        ConversorBalcaoCaixa conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(ModalidadeContrato.BalcaoCaixa);

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*BalcaoCaixa*");
    }

    // ─── ConversorFgi ─────────────────────────────────────────────────────────

    [Fact]
    public void ConversorFgi_retorna_modalidade_Fgi()
    {
        new ConversorFgi().Modalidade.Should().Be(ModalidadeContrato.Fgi);
    }

    [Fact]
    public async Task ConversorFgi_lanca_NotImplementedException()
    {
        ConversorFgi conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(ModalidadeContrato.Fgi);

        Func<Task> act = () => conversor.CriarDetailAsync(ctx, CancellationToken.None);

        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*Fgi*");
    }
}

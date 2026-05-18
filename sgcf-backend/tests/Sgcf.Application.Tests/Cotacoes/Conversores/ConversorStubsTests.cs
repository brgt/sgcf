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
    /// Para modalidades BRL (Nce, CapitalDeGiro, Fgi), PTAX deve ser null.
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

    // ConversorRefinimp: implementação real em Onda 1 — testes em ConversorRefinimpTests.cs.
    // ConversorLei4131: implementação real em Onda 4 — testes em ConversorLei4131Tests.cs.

    // ConversorNce: implementação real entregue na Onda 2 — testes em ConversorNceTests.cs.

    // ─── ConversorCapitalDeGiro ───────────────────────────────────────────────

    [Fact]
    public void ConversorCapitalDeGiro_retorna_modalidade_CapitalDeGiro()
    {
        new ConversorCapitalDeGiro().Modalidade.Should().Be(ModalidadeContrato.CapitalDeGiro);
    }

    /// <summary>
    /// Onda 3b: ConversorCapitalDeGiro implementado — retorna CapitalDeGiroDetail sem lançar NotImplementedException.
    /// Teste de stub substituído por comportamento real.
    /// </summary>
    [Fact]
    public async Task ConversorCapitalDeGiro_retorna_CapitalDeGiroDetail_sem_excecao()
    {
        ConversorCapitalDeGiro conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(ModalidadeContrato.CapitalDeGiro);

        (Entity principal, Entity? secundario) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        principal.Should().BeOfType<CapitalDeGiroDetail>(
            because: "Onda 3b implementou ConversorCapitalDeGiro — retorna CapitalDeGiroDetail");
        secundario.Should().BeNull(because: "Capital de Giro não tem detail secundário");
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

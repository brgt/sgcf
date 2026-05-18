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
/// Testes unitários do ConversorFinimp.
/// Verificam que a extração da lógica de criação de FinimpDetail
/// produz resultado idêntico ao código inline que ela substitui.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConversorFinimpTests
{
    private static readonly Instant AgentInstant = Instant.FromUtc(2026, 5, 18, 9, 0);

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(AgentInstant);
        return clock;
    }

    private static ConverterEmContratoContext CriarContexto(
        string? rofNumero = "ROF-2026-001",
        string? exportadorNome = "ACME Corp",
        string? exportadorPais = "US",
        string? produtoImportado = "Maquinário")
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: new LocalDate(2026, 5, 16),
            dataPtaxReferencia: new LocalDate(2026, 5, 15),
            ptaxUsadaUsdBrl: 5.20m,
            clock: clock);

        cotacao.Enviar(clock);

        Proposta proposta = cotacao.AdicionarProposta(
            bancoId: Guid.NewGuid(),
            moedaOriginal: Moeda.Usd,
            valorOferecidoMoedaOriginal: new Money(100_000m, Moeda.Usd),
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
            dataCaptura: new LocalDate(2026, 5, 16));

        cotacao.EncerrarCaptacao(clock);
        cotacao.AceitarProposta(proposta.Id, "user|test", clock);

        Contrato contrato = Contrato.Criar(
            numeroExterno: "FINIMP-TESTE-001",
            bancoId: proposta.BancoId,
            modalidade: ModalidadeContrato.Finimp,
            valorPrincipal: new Money(100_000m, Moeda.Usd),
            dataContratacao: new LocalDate(2026, 5, 20),
            dataVencimento: new LocalDate(2026, 11, 16),
            taxaAa: Percentual.De(6.5m),
            baseCalculo: BaseCalculo.Dias360,
            clock: clock);

        ConverterEmContratoCommand command = new(
            CotacaoId: cotacao.Id,
            NumeroExternoContrato: "FINIMP-TESTE-001",
            CodigoInternoContrato: null,
            DataContratacao: new DateOnly(2026, 5, 20),
            DataVencimento: new DateOnly(2026, 11, 16),
            TaxaAa: 6.5m,
            Observacoes: null,
            RofNumero: rofNumero,
            ExportadorNome: exportadorNome,
            ExportadorPais: exportadorPais,
            ProdutoImportado: produtoImportado);

        return new ConverterEmContratoContext(cotacao, proposta, contrato, command, clock);
    }

    [Fact]
    public async Task Modalidade_property_retorna_Finimp()
    {
        ConversorFinimp conversor = new();
        conversor.Modalidade.Should().Be(ModalidadeContrato.Finimp);
    }

    [Fact]
    public async Task CriarDetail_com_inputs_completos_retorna_FinimpDetail_populado()
    {
        // Arrange
        ConversorFinimp conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(
            rofNumero: "ROF-2026-001",
            exportadorNome: "ACME Corp",
            exportadorPais: "US",
            produtoImportado: "Maquinário industrial");

        // Act
        (Entity principal, Entity? secundario) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        // Assert
        principal.Should().BeOfType<FinimpDetail>();
        FinimpDetail detail = (FinimpDetail)principal;
        detail.ContratoId.Should().Be(ctx.ContratoCriado.Id);
        detail.RofNumero.Should().Be("ROF-2026-001");
        detail.ExportadorNome.Should().Be("ACME Corp");
        detail.ExportadorPais.Should().Be("US");
        detail.ProdutoImportado.Should().Be("Maquinário industrial");
    }

    [Fact]
    public async Task CriarDetail_com_RofNumero_nulo_persiste_null()
    {
        // Arrange
        ConversorFinimp conversor = new();
        ConverterEmContratoContext ctx = CriarContexto(rofNumero: null);

        // Act
        (Entity principal, _) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        // Assert
        FinimpDetail detail = (FinimpDetail)principal;
        detail.RofNumero.Should().BeNull();
    }

    [Fact]
    public async Task Secundario_eh_sempre_null()
    {
        // Arrange
        ConversorFinimp conversor = new();
        ConverterEmContratoContext ctx = CriarContexto();

        // Act
        (_, Entity? secundario) = await conversor.CriarDetailAsync(ctx, CancellationToken.None);

        // Assert — FINIMP não tem detail secundário (apenas o padrão de assinatura)
        secundario.Should().BeNull();
    }
}

using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes para os invariantes de PTAX nullable introduzidos na Onda 0 F0.1.
/// SPEC docs/specs/cotacoes/modalidades/onda-0.md §3.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CotacaoPtaxNullableTests
{
    private static readonly IClock Clock = PropostaFactory.CriarClockFixo();
    private static readonly LocalDate DataAbertura = new(2026, 5, 15);
    private static readonly LocalDate DataPtax = new(2026, 5, 14);
    private static readonly Money ValorAlvo = new(1_000_000m, Moeda.Brl);
    private const int PrazoDias = 180;

    // ─── Modalidades que EXIGEM PTAX ────────────────────────────────────────

    [Fact]
    public void Criar_FINIMP_sem_PTAX_lanca_excecao()
    {
        // Finimp é modalidade cambial — PTAX obrigatória.
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: PrazoDias,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*PTAX*");
    }

    [Fact]
    public void Criar_REFINIMP_sem_PTAX_lanca_excecao()
    {
        // Refinimp compartilha fórmula FINIMP — PTAX obrigatória.
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: PrazoDias,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*PTAX*");
    }

    [Fact]
    public void Criar_Lei4131_sem_PTAX_lanca_excecao()
    {
        // Lei 4131 é modalidade em moeda estrangeira — PTAX obrigatória.
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Lei4131,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: PrazoDias,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*PTAX*");
    }

    // ─── Modalidades BRL puras — PTAX PROIBIDA ──────────────────────────────

    [Fact]
    public void Criar_NCE_com_PTAX_lanca_excecao()
    {
        // NCE é operação BRL pura — fornecer PTAX é erro semântico.
        // A mensagem deve mencionar BRL ou "não se aplica".
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Nce,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: PrazoDias,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: 5.20m,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    [Fact]
    public void Criar_CapitalDeGiro_com_PTAX_lanca_excecao()
    {
        // CapitalDeGiro é operação BRL pura — fornecer PTAX é erro semântico.
        var act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.CapitalDeGiro,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: PrazoDias,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: 5.20m,
            clock: Clock);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    // ─── Modalidades BRL puras — criação SEM PTAX deve ter sucesso ──────────

    [Fact]
    public void Criar_NCE_sem_PTAX_sucesso()
    {
        // NCE sem PTAX deve criar cotação vigente com PtaxUsadaUsdBrl = null.
        var cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Nce,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: PrazoDias,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: Clock);

        cotacao.Should().NotBeNull();
        cotacao.Status.Should().Be(StatusCotacao.Rascunho);
        cotacao.PtaxUsadaUsdBrl.Should().BeNull();
        cotacao.DataPtaxReferencia.Should().BeNull();
    }

    [Fact]
    public void Criar_Fgi_sem_PTAX_sucesso()
    {
        // FGI é operação BRL pura — deve criar sem PTAX.
        var cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Fgi,
            valorAlvoBrl: ValorAlvo,
            prazoMaximoDias: PrazoDias,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: Clock);

        cotacao.Should().NotBeNull();
        cotacao.PtaxUsadaUsdBrl.Should().BeNull();
        cotacao.DataPtaxReferencia.Should().BeNull();
    }

    // ─── Helper ExigeMoedaEstrangeira ────────────────────────────────────────

    [Theory]
    [InlineData(ModalidadeContrato.Finimp, true)]
    [InlineData(ModalidadeContrato.Refinimp, true)]
    [InlineData(ModalidadeContrato.Lei4131, true)]
    [InlineData(ModalidadeContrato.Nce, false)]
    [InlineData(ModalidadeContrato.CapitalDeGiro, false)]
    [InlineData(ModalidadeContrato.Fgi, false)]
    public void ExigeMoedaEstrangeira_helper_retorna_valor_correto_por_modalidade(
        ModalidadeContrato modalidade,
        bool esperado)
    {
        Cotacao.ExigeMoedaEstrangeira(modalidade).Should().Be(esperado);
    }
}

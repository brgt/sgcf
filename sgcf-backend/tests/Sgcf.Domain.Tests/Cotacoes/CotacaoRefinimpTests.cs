using FluentAssertions;
using NodaTime;
using NSubstitute;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

/// <summary>
/// Testes RED para a propriedade <c>Cotacao.ContratoMaeId</c> (Onda 1 REFINIMP).
/// SPEC §3.1 — invariantes AD-1.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CotacaoRefinimpTests
{
    private static readonly Guid ContratoMaeValido = Guid.NewGuid();

    private static readonly LocalDate DataAbertura = new(2026, 6, 1);
    private static readonly LocalDate DataPtax = new(2026, 5, 31);
    private const decimal PtaxValida = 5.20m;

    private static IClock CriarClock()
    {
        IClock clock = Substitute.For<IClock>();
        clock.GetCurrentInstant().Returns(Instant.FromUtc(2026, 6, 1, 12, 0));
        return clock;
    }

    // ── Cenários REFINIMP com ContratoMaeId ─────────────────────────────────

    [Fact(DisplayName = "Criar REFINIMP sem ContratoMaeId lança ArgumentException")]
    public void Criar_Refinimp_sem_ContratoMaeId_lanca_excecao()
    {
        IClock clock = CriarClock();

        Action act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-R0001",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: PtaxValida,
            clock: clock,
            contratoMaeId: null);  // ausente — deve falhar

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ContratoMaeId*obrigatório*Refinimp*");
    }

    [Fact(DisplayName = "Criar REFINIMP com ContratoMaeId = Guid.Empty lança ArgumentException")]
    public void Criar_Refinimp_com_ContratoMaeId_Empty_lanca_excecao()
    {
        IClock clock = CriarClock();

        Action act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-R0001",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: PtaxValida,
            clock: clock,
            contratoMaeId: Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ContratoMaeId*");
    }

    [Fact(DisplayName = "Criar REFINIMP com ContratoMaeId válido deve ter sucesso e persistir o id")]
    public void Criar_Refinimp_com_ContratoMaeId_valido_sucesso()
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-R0001",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: PtaxValida,
            clock: clock,
            contratoMaeId: ContratoMaeValido);

        cotacao.ContratoMaeId.Should().Be(ContratoMaeValido);
        cotacao.Modalidade.Should().Be(ModalidadeContrato.Refinimp);
    }

    [Fact(DisplayName = "Criar FINIMP com ContratoMaeId informado lança ArgumentException (defesa)")]
    public void Criar_Finimp_com_ContratoMaeId_lanca_excecao()
    {
        IClock clock = CriarClock();

        Action act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: PtaxValida,
            clock: clock,
            contratoMaeId: ContratoMaeValido);  // não aplicável a FINIMP

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ContratoMaeId*não se aplica*Finimp*");
    }

    [Fact(DisplayName = "Criar REFINIMP sem PTAX lança ArgumentException (herda invariante Onda 0)")]
    public void Criar_Refinimp_sem_PTAX_lanca_excecao()
    {
        IClock clock = CriarClock();

        Action act = () => Cotacao.Criar(
            codigoInterno: "COT-2026-R0001",
            modalidade: ModalidadeContrato.Refinimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,  // ausente para modalidade cambial
            clock: clock,
            contratoMaeId: ContratoMaeValido);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*PTAX D-1*obrigatória*Refinimp*");
    }

    [Fact(DisplayName = "Criar FINIMP sem ContratoMaeId continua funcionando (regressão)")]
    public void Criar_Finimp_sem_ContratoMaeId_regressao()
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-00001",
            modalidade: ModalidadeContrato.Finimp,
            valorAlvoBrl: new Money(500_000m, Moeda.Brl),
            prazoMaximoDias: 180,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: DataPtax,
            ptaxUsadaUsdBrl: PtaxValida,
            clock: clock);  // sem contratoMaeId — padrão null

        cotacao.ContratoMaeId.Should().BeNull();
        cotacao.Modalidade.Should().Be(ModalidadeContrato.Finimp);
    }

    [Fact(DisplayName = "Criar NCE sem ContratoMaeId continua funcionando (regressão BRL puro)")]
    public void Criar_Nce_sem_ContratoMaeId_regressao()
    {
        IClock clock = CriarClock();

        Cotacao cotacao = Cotacao.Criar(
            codigoInterno: "COT-2026-N0001",
            modalidade: ModalidadeContrato.Nce,
            valorAlvoBrl: new Money(200_000m, Moeda.Brl),
            prazoMaximoDias: 90,
            dataAbertura: DataAbertura,
            dataPtaxReferencia: null,
            ptaxUsadaUsdBrl: null,
            clock: clock);

        cotacao.ContratoMaeId.Should().BeNull();
        cotacao.Modalidade.Should().Be(ModalidadeContrato.Nce);
    }
}

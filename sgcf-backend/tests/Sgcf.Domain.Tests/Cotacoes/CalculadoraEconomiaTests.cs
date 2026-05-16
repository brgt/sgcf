using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;
using Xunit;

namespace Sgcf.Domain.Tests.Cotacoes;

public sealed class CalculadoraEconomiaTests
{
    private static readonly LocalDate DataReferenciaCdi = new(2026, 5, 15);

    // ─── Caso base: mesmo prazo ──────────────────────────────────────────────

    [Fact]
    public void Calcular_com_mesmo_prazo_deve_retornar_economia_positiva_quando_proposta_mais_cara()
    {
        // CET proposta 7.5% vs contrato 7.2% — diferença de 0.3% a.a.
        // Economia = 0.3%/100 × 5.000.000 × 180/360 = 7.500 BRL
        var (economiaBruta, _, _) = CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            valorPrincipalBrl: new Money(5_000_000m, Moeda.Brl),
            prazoProposta: 180,
            prazoContrato: 180,
            taxaCdiAaPercentual: 10.75m,
            dataReferenciaCdi: DataReferenciaCdi);

        economiaBruta.Moeda.Should().Be(Moeda.Brl);
        economiaBruta.Valor.Should().BeApproximately(7_500m, 1m);
    }

    [Fact]
    public void Calcular_com_mesmo_prazo_e_mesmo_CET_deve_ter_economia_zero()
    {
        var (economiaBruta, _, _) = CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.5m,
            valorPrincipalBrl: new Money(5_000_000m, Moeda.Brl),
            prazoProposta: 180,
            prazoContrato: 180,
            taxaCdiAaPercentual: 10.75m,
            dataReferenciaCdi: DataReferenciaCdi);

        economiaBruta.Valor.Should().Be(0m);
    }

    [Fact]
    public void Calcular_com_contrato_mais_caro_deve_retornar_economia_negativa()
    {
        // Contrato fechado 8% vs proposta 7.5% → perda de 0.5% a.a.
        var (economiaBruta, _, _) = CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 8.0m,
            valorPrincipalBrl: new Money(1_000_000m, Moeda.Brl),
            prazoProposta: 360,
            prazoContrato: 360,
            taxaCdiAaPercentual: 10.75m,
            dataReferenciaCdi: DataReferenciaCdi);

        economiaBruta.Valor.Should().BeNegative();
    }

    // ─── Caso com prazos diferentes ──────────────────────────────────────────

    [Fact]
    public void Calcular_com_prazos_diferentes_deve_retornar_economia_ajustada_nao_nula()
    {
        var (_, economiaAjustada, dataRef) = CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            valorPrincipalBrl: new Money(5_000_000m, Moeda.Brl),
            prazoProposta: 180,
            prazoContrato: 270, // prazo diferente
            taxaCdiAaPercentual: 10.75m,
            dataReferenciaCdi: DataReferenciaCdi);

        economiaAjustada.Moeda.Should().Be(Moeda.Brl);
        dataRef.Should().Be(DataReferenciaCdi);
    }

    [Fact]
    public void Calcular_com_prazos_diferentes_economia_ajustada_diverge_da_bruta()
    {
        var (economiaBruta, economiaAjustada, _) = CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            valorPrincipalBrl: new Money(5_000_000m, Moeda.Brl),
            prazoProposta: 180,
            prazoContrato: 270,
            taxaCdiAaPercentual: 10.75m,
            dataReferenciaCdi: DataReferenciaCdi);

        // Com prazos diferentes, a economia ajustada NÃO deve ser igual à bruta
        economiaAjustada.Valor.Should().NotBe(economiaBruta.Valor);
    }

    // ─── Validações de entrada ───────────────────────────────────────────────

    [Fact]
    public void Calcular_com_moeda_nao_BRL_deve_lancar_excecao()
    {
        var act = () => CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            valorPrincipalBrl: new Money(1_000_000m, Moeda.Usd),
            prazoProposta: 180,
            prazoContrato: 180,
            taxaCdiAaPercentual: 10.75m,
            dataReferenciaCdi: DataReferenciaCdi);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*BRL*");
    }

    [Fact]
    public void Calcular_com_prazo_zero_deve_lancar_excecao()
    {
        var act = () => CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            valorPrincipalBrl: new Money(1_000_000m, Moeda.Brl),
            prazoProposta: 0,
            prazoContrato: 180,
            taxaCdiAaPercentual: 10.75m,
            dataReferenciaCdi: DataReferenciaCdi);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*PrazoProposta*");
    }

    [Fact]
    public void Calcular_com_CDI_zero_deve_lancar_excecao()
    {
        var act = () => CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.5m,
            cetContratoAaPercentual: 7.2m,
            valorPrincipalBrl: new Money(1_000_000m, Moeda.Brl),
            prazoProposta: 180,
            prazoContrato: 180,
            taxaCdiAaPercentual: 0m,
            dataReferenciaCdi: DataReferenciaCdi);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*TaxaCdiAaPercentual*positiva*");
    }

    // ─── DataReferenciaCdi preservada ────────────────────────────────────────

    [Fact]
    public void Calcular_deve_retornar_DataReferenciaCdi_informada()
    {
        var dataRef = new LocalDate(2026, 4, 30);

        var (_, _, dataRetornada) = CalculadoraEconomia.Calcular(
            cetPropostaAaPercentual: 7.0m,
            cetContratoAaPercentual: 6.8m,
            valorPrincipalBrl: new Money(1_000_000m, Moeda.Brl),
            prazoProposta: 90,
            prazoContrato: 90,
            taxaCdiAaPercentual: 10.5m,
            dataReferenciaCdi: dataRef);

        dataRetornada.Should().Be(dataRef);
    }
}

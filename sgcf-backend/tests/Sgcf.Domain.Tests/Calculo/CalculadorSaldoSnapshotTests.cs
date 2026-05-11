using FluentAssertions;
using NodaTime;
using Sgcf.Domain.Calculo;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;
using Xunit;

namespace Sgcf.Domain.Tests.Calculo;

[Trait("Category", "Domain")]
public sealed class CalculadorSaldoSnapshotTests
{
    [Fact]
    public void Calcular_RegressaoSnapshot_ComponentesConhecidos()
    {
        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: new Money(1_000_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataReferencia: new LocalDate(2026, 5, 7),
            Eventos: new List<EventoSaldoItem>(),
            TaxaCambio: null);

        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        resultado.SaldoPrincipalAberto.Valor.Should().Be(1_000_000m);
        resultado.SaldoPrincipalAberto.Moeda.Should().Be(Moeda.Usd);
        resultado.JurosProvisionados.Valor.Should().Be(21_000m);
        resultado.ComissoesAPagar.Valor.Should().Be(0m);
        resultado.SaldoTotal.Valor.Should().Be(1_021_000m);
        resultado.SaldoPrincipalAbertoBrl.Should().BeNull();
        resultado.JurosProvisionadosBrl.Should().BeNull();
        resultado.ComissoesAPagarBrl.Should().BeNull();
        resultado.SaldoTotalBrl.Should().BeNull();
    }

    [Fact]
    public void Calcular_RegressaoSnapshot_ComTaxaCambio_BrlCalculado()
    {
        EntradaCalculoSaldo entrada = new(
            ValorPrincipalInicial: new Money(1_000_000m, Moeda.Usd),
            TaxaAa: Percentual.De(6m),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: new LocalDate(2026, 1, 1),
            DataReferencia: new LocalDate(2026, 5, 7),
            Eventos: new List<EventoSaldoItem>(),
            TaxaCambio: 5.00m);

        ResultadoSaldo resultado = CalculadorSaldo.Calcular(entrada);

        resultado.SaldoPrincipalAbertoBrl.Should().NotBeNull();
        resultado.SaldoPrincipalAbertoBrl!.Value.Moeda.Should().Be(Moeda.Brl);
        resultado.SaldoTotalBrl.Should().NotBeNull();
        resultado.SaldoTotalBrl!.Value.Valor.Should().Be(1_021_000m * 5.00m);
    }
}

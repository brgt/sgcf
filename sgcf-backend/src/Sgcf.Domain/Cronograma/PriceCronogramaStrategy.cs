using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

internal sealed class PriceCronogramaStrategy : ICronogramaStrategy
{
    public IReadOnlyList<EventoCronogramaGerado> Gerar(GerarCronogramaInput input)
    {
        // Convert annual rate to monthly: i_m = (1 + i_a)^(1/12) - 1
        decimal taxaMensalDecimal = (decimal)Math.Pow((double)(1m + input.TaxaAa.AsDecimal), 1.0 / 12.0) - 1m;

        EntradaPrice entrada = new(
            ValorPrincipal: input.ValorPrincipal,
            TaxaMensal: Percentual.DeFracao(taxaMensalDecimal),
            DataDesembolso: input.DataDesembolso,
            DataPrimeiroVencimento: input.DataPrimeiroVencimento,
            NumeroParcelas: input.QuantidadeParcelas,
            AliqIrrf: input.AliqIrrf);

        IReadOnlyList<EventoGeradoPrice> gerados = PriceStrategy.Gerar(entrada);

        List<EventoCronogramaGerado> result = new(gerados.Count);
        foreach (EventoGeradoPrice g in gerados)
        {
            result.Add(new EventoCronogramaGerado(g.NumeroParcela, g.Tipo, g.DataPrevista, g.Valor, g.SaldoDevedorApos));
        }

        return result.AsReadOnly();
    }
}

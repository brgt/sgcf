using NodaTime;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cronograma;

internal sealed class SacCronogramaStrategy : ICronogramaStrategy
{
    public IReadOnlyList<EventoCronogramaGerado> Gerar(GerarCronogramaInput input)
    {
        // SAC distributes evenly from DataDesembolso to DataVencimento.
        // DataVencimento = DataPrimeiroVencimento + (n-1) periods.
        LocalDate dataVencimento = input.DataPrimeiroVencimento
            .PlusMonths((input.QuantidadeParcelas - 1) * MesesPorPeriodicidade(input.Periodicidade));

        EntradaSac entrada = new(
            ValorPrincipal: input.ValorPrincipal,
            TaxaAa: input.TaxaAa,
            BaseCalculo: input.BaseCalculo,
            DataDesembolso: input.DataDesembolso,
            DataVencimento: dataVencimento,
            NumeroParcelas: input.QuantidadeParcelas,
            AliqIrrf: input.AliqIrrf);

        IReadOnlyList<EventoGeradoSac> gerados = SacStrategy.Gerar(entrada);

        List<EventoCronogramaGerado> result = new(gerados.Count);
        foreach (EventoGeradoSac g in gerados)
        {
            result.Add(new EventoCronogramaGerado(g.NumeroParcela, g.Tipo, g.DataPrevista, g.Valor, g.SaldoDevedorApos));
        }

        return result.AsReadOnly();
    }

    private static int MesesPorPeriodicidade(Periodicidade periodicidade) =>
        periodicidade switch
        {
            Periodicidade.Mensal => 1,
            Periodicidade.Bimestral => 2,
            Periodicidade.Trimestral => 3,
            Periodicidade.Quadrimestral => 4,
            Periodicidade.Semestral => 6,
            Periodicidade.Anual => 12,
            _ => throw new ArgumentException(
                $"Periodicidade '{periodicidade}' não é suportada para amortização SAC.", nameof(periodicidade))
        };
}

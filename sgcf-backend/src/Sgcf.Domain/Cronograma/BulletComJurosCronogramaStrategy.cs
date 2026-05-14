using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cronograma;

internal sealed class BulletComJurosCronogramaStrategy : ICronogramaStrategy
{
    public IReadOnlyList<EventoCronogramaGerado> Gerar(GerarCronogramaInput input)
    {
        if (input.PeriodicidadeJuros is null)
        {
            throw new ArgumentException(
                "PeriodicidadeJuros é obrigatório para amortização BulletComJurosPeriodicos.", nameof(input));
        }

        EntradaBulletComJuros entrada = new(
            ValorPrincipal: input.ValorPrincipal,
            TaxaAa: input.TaxaAa,
            BaseCalculo: input.BaseCalculo,
            DataDesembolso: input.DataDesembolso,
            DataVencimento: input.DataPrimeiroVencimento,
            PeriodoJurosMeses: MesesPorPeriodicidade(input.PeriodicidadeJuros.Value),
            AliqIrrf: input.AliqIrrf,
            AliqIofCambio: input.AliqIofCambio,
            TarifaRofBrl: input.TarifaRofBrl,
            TarifaCadempBrl: input.TarifaCadempBrl);

        IReadOnlyList<EventoGeradoBulletComJuros> gerados = BulletComJurosPeriodicosStrategy.Gerar(entrada);

        List<EventoCronogramaGerado> result = new(gerados.Count);
        foreach (EventoGeradoBulletComJuros g in gerados)
        {
            result.Add(new EventoCronogramaGerado(g.NumeroEvento, g.Tipo, g.DataPrevista, g.Valor, g.SaldoDevedorApos));
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
                $"Periodicidade '{periodicidade}' não é suportada para juros periódicos.", nameof(periodicidade))
        };
}

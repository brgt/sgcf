namespace Sgcf.Domain.Cronograma;

internal sealed class BulletCronogramaStrategy : ICronogramaStrategy
{
    public IReadOnlyList<EventoCronogramaGerado> Gerar(GerarCronogramaInput input)
    {
        EntradaBullet entrada = new(
            ValorPrincipal: input.ValorPrincipal,
            TaxaAa: input.TaxaAa,
            BaseCalculo: input.BaseCalculo,
            DataDesembolso: input.DataDesembolso,
            DataVencimento: input.DataPrimeiroVencimento,
            AliqIrrf: input.AliqIrrf,
            AliqIofCambio: input.AliqIofCambio,
            TarifaRofBrl: input.TarifaRofBrl,
            TarifaCadempBrl: input.TarifaCadempBrl);

        IReadOnlyList<EventoGeradoBullet> gerados = BulletStrategy.Gerar(entrada);

        List<EventoCronogramaGerado> result = new(gerados.Count);
        foreach (EventoGeradoBullet g in gerados)
        {
            result.Add(new EventoCronogramaGerado(g.NumeroEvento, g.Tipo, g.DataPrevista, g.Valor, g.SaldoDevedorApos));
        }

        return result.AsReadOnly();
    }
}

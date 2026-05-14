using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Gerador de cronograma customizado: valida e normaliza parcelas definidas externamente.
/// Não há geração algorítmica — datas e valores são fornecidos pelo chamador.
/// Funções puras — sem I/O, sem efeitos colaterais.
/// </summary>
public static class CustomizadaStrategy
{
    /// <summary>
    /// Valida e transforma as parcelas customizadas em eventos ordenados por data.
    /// </summary>
    public static IReadOnlyList<EventoGeradoCustomizado> Gerar(EntradaCustomizada entrada)
    {
        ValidarEntrada(entrada);

        // Ordena por DataPrevista; dentro da mesma data, JUROS antes de PRINCIPAL
        List<ParcelaCustomizada> parcelasOrdenadas = [.. entrada.Parcelas
            .OrderBy(p => p.DataPrevista)];

        decimal saldoRestante = entrada.ValorPrincipal.Valor;
        List<EventoGeradoCustomizado> eventos = new(entrada.Parcelas.Count * 2);

        for (int i = 0; i < parcelasOrdenadas.Count; i++)
        {
            ParcelaCustomizada parcela = parcelasOrdenadas[i];
            bool isUltima = i == parcelasOrdenadas.Count - 1;

            saldoRestante = Math.Round(
                saldoRestante - parcela.ValorPrincipal.Valor,
                2,
                MidpointRounding.AwayFromZero);

            // Força saldo zero na última parcela para eliminar erros de arredondamento acumulados
            decimal saldoApos = isUltima ? 0m : saldoRestante;

            eventos.Add(new EventoGeradoCustomizado(
                NumeroParcela: parcela.Numero,
                Tipo: TipoEventoCronograma.Juros,
                DataPrevista: parcela.DataPrevista,
                Valor: parcela.ValorJuros,
                SaldoDevedorApos: null));

            eventos.Add(new EventoGeradoCustomizado(
                NumeroParcela: parcela.Numero,
                Tipo: TipoEventoCronograma.Principal,
                DataPrevista: parcela.DataPrevista,
                Valor: parcela.ValorPrincipal,
                SaldoDevedorApos: saldoApos));
        }

        return eventos.AsReadOnly();
    }

    private static void ValidarEntrada(EntradaCustomizada e)
    {
        if (e.Parcelas.Count == 0)
        {
            throw new ArgumentException(
                "Parcelas não pode ser vazia.",
                nameof(e));
        }

        if (e.ValorPrincipal.Valor <= 0m)
        {
            throw new ArgumentException(
                "ValorPrincipal.Valor deve ser maior que zero.",
                nameof(e));
        }

        foreach (ParcelaCustomizada parcela in e.Parcelas)
        {
            if (parcela.Numero < 1)
            {
                throw new ArgumentException(
                    $"Número de parcela inválido: {parcela.Numero}. Deve ser maior ou igual a 1.",
                    nameof(e));
            }

            if (parcela.ValorPrincipal.Moeda != e.ValorPrincipal.Moeda)
            {
                throw new ArgumentException(
                    $"Moeda da parcela {parcela.Numero} ({parcela.ValorPrincipal.Moeda}) " +
                    $"diverge da moeda da entrada ({e.ValorPrincipal.Moeda}).",
                    nameof(e));
            }
        }
    }
}

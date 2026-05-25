using System.Globalization;
using System.Text;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Formata a coleção de garantias exigidas de um <see cref="LimiteBanco"/> em string legível
/// para preencher o campo <c>GarantiaExigida</c> de uma <see cref="Proposta"/>.
///
/// Exemplos de saída:
/// <list type="bullet">
///   <item><c>"CDB cativo 20% (obrigatório)"</c></item>
///   <item><c>"Aval (obrigatório)"</c></item>
///   <item><c>"SBLC R$ 200.000,00 (obrigatório)"</c></item>
///   <item><c>"CDB cativo 20% (obrigatório) + Aval (obrigatório)"</c></item>
/// </list>
///
/// Pure static — sem estado, sem I/O, sem efeitos colaterais.
/// </summary>
public static class FormatadorGarantiaExigida
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>
    /// Converte a coleção de garantias exigidas em string legível para operadores.
    /// Retorna <see cref="string.Empty"/> quando a coleção for vazia.
    /// </summary>
    /// <param name="garantias">Coleção de garantias do <see cref="LimiteBanco"/>.</param>
    public static string Formatar(IReadOnlyCollection<GarantiaExigidaItem> garantias)
    {
        ArgumentNullException.ThrowIfNull(garantias);

        if (garantias.Count == 0)
        {
            return string.Empty;
        }

        var partes = new List<string>(garantias.Count);
        foreach (GarantiaExigidaItem garantia in garantias)
        {
            partes.Add(FormatarItem(garantia));
        }

        return string.Join(" + ", partes);
    }

    private static string FormatarItem(GarantiaExigidaItem garantia)
    {
        string tipoLabel = TraduzirTipo(garantia.Tipo);
        string obrigatoriedade = garantia.Obrigatoria ? "obrigatório" : "opcional";
        string detalheValor = FormatarDetalheValor(garantia);

        var sb = new StringBuilder(tipoLabel);

        if (!string.IsNullOrEmpty(detalheValor))
        {
            sb.Append(' ');
            sb.Append(detalheValor);
        }

        sb.Append(" (");
        sb.Append(obrigatoriedade);
        sb.Append(')');

        return sb.ToString();
    }

    private static string FormatarDetalheValor(GarantiaExigidaItem garantia)
    {
        if (garantia.PercentualSobreLimite.HasValue)
        {
            // Exibe como número inteiro quando não tem casas decimais (ex: 20 → "20%", 20.5 → "20,5%")
            decimal pct = garantia.PercentualSobreLimite.Value;
            string pctStr = pct == Math.Floor(pct)
                ? ((int)pct).ToString(PtBr)
                : pct.ToString("G", PtBr);

            return $"{pctStr}%";
        }

        if (garantia.ValorFixoBrl.HasValue)
        {
            return garantia.ValorFixoBrl.Value.Valor.ToString("C", PtBr);
        }

        // Aval e outros tipos sem quantificador explícito
        return string.Empty;
    }

    /// <summary>Traduz <see cref="TipoGarantia"/> para rótulo amigável em português.</summary>
    private static string TraduzirTipo(TipoGarantia tipo) => tipo switch
    {
        TipoGarantia.CdbCativo          => "CDB cativo",
        TipoGarantia.Sblc               => "SBLC",
        TipoGarantia.Aval               => "Aval",
        TipoGarantia.AlienacaoFiduciaria => "Alienação Fiduciária",
        TipoGarantia.Duplicatas         => "Duplicatas",
        TipoGarantia.RecebiveisCartao   => "Recebíveis de cartão",
        TipoGarantia.BoletoBancario     => "Boleto bancário",
        TipoGarantia.Fgi                => "FGI",
        _                               => tipo.ToString()
    };
}

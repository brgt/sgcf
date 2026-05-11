using NodaTime;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao;

/// <summary>
/// Valida restrições bancárias antes de executar uma simulação de antecipação.
/// Pure static — sem I/O, sem efeitos colaterais.
/// </summary>
public static class AntecipacaoValidador
{
    /// <summary>
    /// Verifica as regras de negócio do banco e retorna se a antecipação é permitida
    /// e a lista de alertas (restrições bloqueantes e avisos informativos).
    /// </summary>
    public static (bool Permitido, IReadOnlyList<string> Alertas) Validar(
        Banco banco,
        EntradaSimulacaoAntecipacao entrada,
        LocalDate dataEfetiva,
        LocalDate hoje)
    {
        List<string> alertas = new();
        bool permitido = true;

        // Aviso prévio mínimo (usa dias corridos como aproximação — contratos especificam dias úteis)
        if (banco.AvisoPrevioMinDiasUteis > 0)
        {
            int diasAteEfetiva = Period.Between(hoje, dataEfetiva, PeriodUnits.Days).Days;
            if (diasAteEfetiva < banco.AvisoPrevioMinDiasUteis)
            {
                alertas.Add(
                    $"RESTRIÇÃO: Aviso prévio insuficiente. Banco exige {banco.AvisoPrevioMinDiasUteis} dias úteis de antecedência; " +
                    $"data efetiva está a {diasAteEfetiva} dias corridos.");
                permitido = false;
            }
        }

        bool ehParcial = entrada.Tipo is TipoAntecipacao.LiquidacaoParcialReducaoPrazo
                      or TipoAntecipacao.LiquidacaoParcialReducaoParcela
                      or TipoAntecipacao.AmortizacaoExtraordinariaAvulsa;

        // Valor mínimo parcial (informacional — não bloqueia pois não conhecemos o saldo total aqui)
        if (banco.ValorMinimoParcialPct.HasValue && ehParcial)
        {
            alertas.Add(
                $"Atenção: Este banco exige mínimo de {banco.ValorMinimoParcialPct.Value.AsHumano:F1}% do saldo principal " +
                "para liquidação parcial. Verifique se o valor solicitado atende este critério.");
        }

        // Exige parcela inteira (bloqueante para liquidações parciais)
        if (banco.ExigeParcelaInteira && ehParcial)
        {
            alertas.Add(
                "RESTRIÇÃO: Este banco exige que a amortização parcial corresponda a uma ou mais parcelas inteiras, " +
                "sem fracionamento. Ajuste o valor para coincidir com parcelas completas do cronograma.");
            permitido = false;
        }

        // Anuência expressa (informacional — não bloqueia a simulação, mas deve ser obtida antes da execução)
        if (banco.ExigeAnuenciaExpressa)
        {
            alertas.Add(
                "Atenção: Este banco exige anuência expressa para antecipação. " +
                "Obtenha aprovação formal do banco antes de efetivar o pagamento.");
        }

        return (permitido, alertas.AsReadOnly());
    }
}

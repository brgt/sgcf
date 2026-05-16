using NodaTime;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Serviço de domínio puro para cálculo do CET (Custo Efetivo Total) de uma Proposta.
/// Sem I/O, sem estado, sem IClock — função matemática pura.
/// Reutiliza o motor de amortização em Sgcf.Domain.Cronograma.
/// SPEC §5.1.
/// </summary>
public static class CalculadoraCet
{
    private const int MaxIteracoesNewtonRaphson = 200;
    private const decimal ToleranciaConvergencia = 0.000_000_01m; // 1e-8

    /// <summary>
    /// Calcula o CET anualizado em percentual (ex: 7.5 para 7,5% a.a.)
    /// para uma proposta de FINIMP.
    /// </summary>
    /// <param name="proposta">Proposta com taxa, estrutura e demais parâmetros.</param>
    /// <param name="ptaxUsdBrl">Taxa PTAX D-1 USD/BRL para conversão dos fluxos.</param>
    /// <param name="dataDesembolso">Data de desembolso (início do fluxo).</param>
    /// <param name="taxaAaPercentualOverride">
    /// Quando informado, substitui <see cref="Proposta.TaxaAaPercentualDecimal"/> no cálculo.
    /// Necessário para calcular o CET do contrato fechado com taxa final negociada
    /// sem mutar a proposta original (SPEC §5.2).
    /// </param>
    /// <returns>CET em % a.a. (ex: 7.5m para 7,5%).</returns>
    public static decimal CalcularCet(
        Proposta proposta,
        decimal ptaxUsdBrl,
        LocalDate dataDesembolso,
        decimal? taxaAaPercentualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        if (ptaxUsdBrl <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ptaxUsdBrl), "PtaxUsdBrl deve ser positiva.");
        }

        // ── 1. Converter ValorOferecido para BRL via PTAX ────────────────────
        Money valorBrl = ConverterParaBrl(proposta.ValorOferecidoMoedaOriginal, ptaxUsdBrl);

        // ── 2. Projetar fluxo usando motor de amortização ────────────────────
        LocalDate dataVencimento = dataDesembolso.PlusDays(proposta.PrazoDias);
        decimal taxaBase = taxaAaPercentualOverride ?? proposta.TaxaAaPercentualDecimal;
        decimal taxaEfetiva = taxaBase + proposta.SpreadAaPercentualDecimal;

        IReadOnlyList<EventoCronogramaGerado> eventos = ProjetarFluxo(
            proposta,
            valorBrl,
            taxaEfetiva,
            dataDesembolso,
            dataVencimento);

        // ── 3. Montar fluxo de caixa em BRL (dia → valor) ───────────────────
        // t=0: saída do principal (negativo = desembolso do tomador)
        // t>0: entradas de pagamentos (positivo = recebimento do tomador)
        List<(int DiasFromT0, decimal FluxoBrl)> fluxos = MontarFluxoBrl(
            eventos,
            dataDesembolso,
            valorBrl,
            proposta,
            ptaxUsdBrl);

        // ── 4. Calcular TIR sobre o fluxo e anualizar ────────────────────────
        decimal tirDiaria = CalcularTirDiaria(fluxos);
        decimal cetAa = AnualizarTaxaDiaria(tirDiaria, proposta.PrazoDias);

        // CET tem floor em 0%: o rendimento da garantia (CDB cativo) reduz o custo
        // do empréstimo mas não pode torná-lo lucrativo para o tomador — o rendimento
        // pertence ao banco durante o bloqueio. Sem este floor, garantias ≥ 100% do
        // principal produziam CET negativo (semanticamente errado). Ver SPEC §5.1.
        decimal cetAjustado = Math.Max(0m, cetAa);

        return Math.Round(cetAjustado * 100m, 6, MidpointRounding.AwayFromZero);
    }

    // ─── Helpers internos ───────────────────────────────────────────────────

    private static Money ConverterParaBrl(Money valor, decimal ptaxUsdBrl)
    {
        if (valor.Moeda == Moeda.Brl)
        {
            return valor;
        }

        // Para MVP FINIMP: moedas não-BRL são convertidas via USD como referência.
        // Cross-rates: EUR, CNY, JPY → USD → BRL.
        // No MVP, ptaxUsdBrl é a única taxa disponível; para outras moedas seria necessário
        // cross-rate explícito. Decisão de design: aceitar como USD-equivalente no MVP
        // (registrado no relatório final como ponto para Onda 2).
        return new Money(
            Math.Round(valor.Valor * ptaxUsdBrl, 6, MidpointRounding.AwayFromZero),
            Moeda.Brl);
    }

    private static IReadOnlyList<EventoCronogramaGerado> ProjetarFluxo(
        Proposta proposta,
        Money valorBrl,
        decimal taxaEfetiva,
        LocalDate dataDesembolso,
        LocalDate dataVencimento)
    {
        // Usa o motor de amortização existente para gerar o fluxo hipotético.
        // Para calcular CET, trabalhamos na moeda funcional (BRL) com a taxa total (taxa + spread).
        // Periodicidade Bullet (prazo único) é o padrão FINIMP; Price e SAC também suportados.
        // taxaEfetiva está em % a.a. "humano" (ex: 6.5 para 6,5%); Percentual.De converte para fração.
        var entrada = new GerarCronogramaInput(
            ValorPrincipal: valorBrl,
            TaxaAa: Percentual.De(taxaEfetiva),
            BaseCalculo: BaseCalculo.Dias360,
            DataDesembolso: dataDesembolso,
            DataPrimeiroVencimento: dataVencimento,
            QuantidadeParcelas: CalcularQuantidadeParcelas(proposta.EstruturaAmortizacao, proposta.PrazoDias),
            Periodicidade: proposta.EstruturaAmortizacao == EstruturaAmortizacao.Bullet
                ? Periodicidade.Bullet
                : proposta.PeriodicidadeJuros,
            AnchorDiaMes: AnchorDiaMes.DiaContratacao,
            AnchorDiaFixo: null,
            PeriodicidadeJuros: proposta.PeriodicidadeJuros,
            ConvencaoDataNaoUtil: ConvencaoDataNaoUtil.Following);

        ICronogramaStrategy strategy = CronogramaStrategyFactory.Criar(proposta.EstruturaAmortizacao);
        return strategy.Gerar(entrada);
    }

    private static int CalcularQuantidadeParcelas(EstruturaAmortizacao estrutura, int prazoDias)
    {
        // Para estruturas non-bullet, estima parcelas mensais como approximação.
        // Onda 2 pode refinar com entrada explícita de parcelas.
        return estrutura == EstruturaAmortizacao.Bullet
            ? 1
            : Math.Max(1, (int)Math.Round(prazoDias / 30.0, MidpointRounding.AwayFromZero));
    }

    private static List<(int DiasFromT0, decimal FluxoBrl)> MontarFluxoBrl(
        IReadOnlyList<EventoCronogramaGerado> eventos,
        LocalDate dataDesembolso,
        Money principalBrl,
        Proposta proposta,
        decimal ptaxUsdBrl)
    {
        var fluxos = new List<(int, decimal)>(eventos.Count + 3);

        // t=0: desembolso do tomador (valor negativo — saída de caixa)
        fluxos.Add((0, -principalBrl.Valor));

        // t=0: IOF sobre principal (custo adicional em t=0)
        if (proposta.IofPercentualDecimal > 0)
        {
            decimal iof = Math.Round(
                principalBrl.Valor * proposta.IofPercentualDecimal / 100m,
                6,
                MidpointRounding.AwayFromZero);
            fluxos.Add((0, iof));
        }

        // t=0: Custo NDF (se exigido) — custo sobre o prazo, pago adiantado
        // Decisão de design: NDF tratado como custo upfront em t=0 (simplificação MVP).
        // SPEC §5.1 diz "aplica sobre principal × prazo" mas não especifica timing.
        if (proposta.ExigeNdf && proposta.CustoNdfAaPercentualDecimal.HasValue)
        {
            decimal custoNdf = Math.Round(
                principalBrl.Valor
                    * proposta.CustoNdfAaPercentualDecimal.Value / 100m
                    * proposta.PrazoDias / 360m,
                6,
                MidpointRounding.AwayFromZero);
            fluxos.Add((0, custoNdf));
        }

        // t=0: Rendimento CDB cativo (se aplicável) — SUBTRAI do custo efetivo
        // Modelado como receita em t=0 para simplificar (equivalente ao VPL do rendimento).
        // Onda 2 pode refinar para distribuir ao longo do prazo.
        if (proposta.GarantiaEhCdbCativo && proposta.RendimentoCdbAaPercentualDecimal.HasValue)
        {
            decimal rendimentoCdb = Math.Round(
                proposta.ValorGarantiaExigidaBrlDecimal
                    * proposta.RendimentoCdbAaPercentualDecimal.Value / 100m
                    * proposta.PrazoDias / 360m,
                6,
                MidpointRounding.AwayFromZero);
            // Rendimento reduz custo: sinal negativo na saída de caixa
            fluxos.Add((0, -rendimentoCdb));
        }

        // Eventos do cronograma (pagamentos futuros — entradas para o tomador)
        foreach (EventoCronogramaGerado evento in eventos)
        {
            if (evento.Tipo is TipoEventoCronograma.Principal or TipoEventoCronograma.Juros)
            {
                int diasDesdeT0 = Period.Between(dataDesembolso, evento.DataPrevista, PeriodUnits.Days).Days;

                if (diasDesdeT0 <= 0)
                {
                    continue; // eventos em t=0 já tratados acima
                }

                Money valorBrl = ConverterParaBrl(evento.Valor, ptaxUsdBrl);
                fluxos.Add((diasDesdeT0, valorBrl.Valor));
            }
        }

        return fluxos;
    }

    /// <summary>
    /// Calcula a Taxa Interna de Retorno diária usando Newton-Raphson.
    /// VPL(r) = Σ Fᵢ / (1+r)^tᵢ = 0, onde r é a taxa diária.
    /// </summary>
    private static decimal CalcularTirDiaria(List<(int DiasFromT0, decimal FluxoBrl)> fluxos)
    {
        // Chute inicial: taxa equivalente a 8% a.a. em base diária
        decimal r = (decimal)Math.Pow(1.08, 1.0 / 360.0) - 1m;

        for (int iteracao = 0; iteracao < MaxIteracoesNewtonRaphson; iteracao++)
        {
            decimal vpl = 0m;
            decimal dvpl = 0m; // derivada em relação a r

            foreach ((int t, decimal f) in fluxos)
            {
                if (t == 0)
                {
                    vpl += f;
                    // derivada de f/(1+r)^0 = f → derivada = 0
                    continue;
                }

                double fator = Math.Pow((double)(1m + r), t);
                decimal desconto = (decimal)(1.0 / fator);

                vpl += f * desconto;
                dvpl += -t * f * desconto / (1m + r);
            }

            if (Math.Abs(dvpl) < ToleranciaConvergencia)
            {
                break; // convergiu ou derivada degenerada
            }

            decimal delta = vpl / dvpl;
            r -= delta;

            if (Math.Abs(delta) < ToleranciaConvergencia)
            {
                break; // convergiu
            }
        }

        return r;
    }

    /// <summary>
    /// Anualiza taxa diária para base 360 dias (convenção FINIMP/comercial).
    /// Formula: (1 + r_diária)^360 − 1.
    /// </summary>
    private static decimal AnualizarTaxaDiaria(decimal taxaDiaria, int prazoDias)
    {
        // Para MVP usa base 360 conforme FINIMP (BaseCalculo.Dias360).
        double taxaAnual = Math.Pow((double)(1m + taxaDiaria), 360.0) - 1.0;
        return (decimal)taxaAnual;
    }
}

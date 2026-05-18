using NodaTime;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Inputs específicos do FGI que não se encaixam nos parâmetros gerais de Proposta.
/// Onda 0 F0.2 — placeholder; implementação real virá na SPEC de FGI.
/// </summary>
/// <param name="TaxaFgiAaPercentual">Taxa anual cobrada pelo Fundo Garantidor de Investimentos sobre o saldo devedor.</param>
/// <param name="PercentualCoberto">Percentual do principal coberto pelo FGI (ex: 80 para 80%). Null se não aplicável.</param>
public sealed record FgiInputs(decimal TaxaFgiAaPercentual, decimal? PercentualCoberto);

/// <summary>
/// Serviço de domínio puro para cálculo do CET (Custo Efetivo Total) de uma Proposta.
/// Sem I/O, sem estado, sem IClock — função matemática pura.
/// Reutiliza o motor de amortização em Sgcf.Domain.Cronograma.
/// SPEC §5.1. Onda 0 F0.2: fachada dispatcheia por modalidade via ptaxUsadaUsdBrl.
/// </summary>
public static class CalculadoraCet
{
    private const int MaxIteracoesNewtonRaphson = 200;
    private const decimal ToleranciaConvergencia = 0.000_000_01m; // 1e-8

    // ─── Fachada pública ────────────────────────────────────────────────────

    /// <summary>
    /// Fachada de cálculo do CET. Dispatcheia para o método especializado correto
    /// com base na presença de PTAX (indicador da família de modalidade):
    /// <list type="bullet">
    ///   <item>ptaxUsadaUsdBrl não-null → modalidade cambial (FINIMP/REFINIMP/Lei4131) → <see cref="CalcularCetFinimp"/>.</item>
    ///   <item>ptaxUsadaUsdBrl null → modalidade BRL pura (NCE, Capital de Giro, FGI) → NotImplementedException até Onda futura.</item>
    /// </list>
    /// <para>
    /// Onda 0 F0.2: aceita <see cref="decimal?"/> para eliminar o adapter <c>?? 1m</c> introduzido em F0.1.
    /// </para>
    /// </summary>
    /// <param name="proposta">Proposta com taxa, estrutura e demais parâmetros.</param>
    /// <param name="ptaxUsadaUsdBrl">
    /// Taxa PTAX D-1 USD/BRL. Não-null para modalidades cambiais (FINIMP, REFINIMP, Lei4131);
    /// null para modalidades BRL puras (NCE, Capital de Giro, FGI).
    /// </param>
    /// <param name="dataDesembolso">Data de desembolso (início do fluxo).</param>
    /// <param name="taxaAaPercentualOverride">
    /// Quando informado, substitui <see cref="Proposta.TaxaAaPercentualDecimal"/> no cálculo.
    /// Necessário para calcular o CET do contrato fechado com taxa final negociada
    /// sem mutar a proposta original (SPEC §5.2).
    /// </param>
    /// <returns>CET em % a.a. (ex: 7.5m para 7,5%).</returns>
    public static decimal CalcularCet(
        Proposta proposta,
        decimal? ptaxUsadaUsdBrl,
        LocalDate dataDesembolso,
        decimal? taxaAaPercentualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        // Dispatch: presença de PTAX identifica a família de modalidade.
        // Modalidades cambiais (FINIMP, REFINIMP, Lei4131) sempre têm PTAX não-null
        // por invariante de domínio em Cotacao.Criar.
        if (ptaxUsadaUsdBrl is not null)
        {
            return CalcularCetFinimp(proposta, ptaxUsadaUsdBrl.Value, dataDesembolso, taxaAaPercentualOverride);
        }

        // PTAX null → modalidade BRL pura.
        // Onda 2: NCE implementado. Capital de Giro (Onda 3) e FGI (Onda 3) ainda pendentes.
        if (proposta.MoedaOriginal == Moeda.Brl)
        {
            return CalcularCetNce(proposta, dataDesembolso, taxaAaPercentualOverride);
        }

        throw new NotImplementedException(
            "Cálculo de CET para modalidades BRL puras não-NCE (Capital de Giro, FGI) " +
            "será implementado nas Ondas específicas de cada modalidade. " +
            "Veja docs/specs/cotacoes/modalidades/ para o roadmap.");
    }

    // ─── Métodos especializados por modalidade ──────────────────────────────

    /// <summary>
    /// Calcula o CET anualizado em percentual (ex: 7.5 para 7,5% a.a.)
    /// para propostas da modalidade FINIMP.
    /// Inputs: moeda estrangeira (USD/CNY/EUR), NDF opcional, BreakFunding.
    /// Onda 0 F0.2: extração exata da lógica que antes estava em <see cref="CalcularCet"/>.
    /// Comportamento bit-a-bit idêntico ao legado — golden dataset não deve regredir.
    /// </summary>
    /// <param name="proposta">Proposta FINIMP com parâmetros cambiais.</param>
    /// <param name="ptaxUsdBrl">Taxa PTAX D-1 USD/BRL; deve ser positiva.</param>
    /// <param name="dataDesembolso">Data de desembolso (início do fluxo).</param>
    /// <param name="taxaAaPercentualOverride">
    /// Substitui <see cref="Proposta.TaxaAaPercentualDecimal"/> quando informado.
    /// Usado para calcular CET do contrato fechado com taxa final negociada (SPEC §5.2).
    /// </param>
    /// <returns>CET em % a.a.</returns>
    public static decimal CalcularCetFinimp(
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

    /// <summary>
    /// Calcula o CET para propostas REFINIMP.
    /// REFINIMP é FINIMP refinanciado — usa fórmula CET idêntica (SPEC §4.2, MD-8).
    /// Onda 0 F0.2.
    /// </summary>
    /// <inheritdoc cref="CalcularCetFinimp"/>
    public static decimal CalcularCetRefinimp(
        Proposta proposta,
        decimal ptaxUsdBrl,
        LocalDate dataDesembolso,
        decimal? taxaAaPercentualOverride = null) =>
        CalcularCetFinimp(proposta, ptaxUsdBrl, dataDesembolso, taxaAaPercentualOverride);

    /// <summary>
    /// Calcula o CET anualizado em percentual (ex: 8.42 para 8,42% a.a.)
    /// para propostas da modalidade Lei 4131/62 (empréstimo direto do exterior).
    /// <para>
    /// A fórmula é idêntica a FINIMP (SPEC §7.3): fluxo em moeda original convertido
    /// para BRL via <paramref name="ptaxUsdBrl"/>, TIR Newton-Raphson, base 360 dias.
    /// A diferença é que Lei 4131 rejeita BRL como moeda original (sempre estrangeira).
    /// </para>
    /// <para>
    /// Componentes que NÃO entram no CET (decisões travadas MD-3/AD-3):
    /// IRRF (informativo via <c>irrfEstimadoBrl</c>), custo SBLC, break funding fee, market flex.
    /// </para>
    /// Onda 4 — SPEC §7.1 (docs/specs/cotacoes/modalidades/lei4131.md).
    /// </summary>
    /// <param name="proposta">Proposta Lei 4131 com MoedaOriginal != Brl.</param>
    /// <param name="ptaxUsdBrl">PTAX USD/BRL da cotação (ou cross-rate efetivo para outras moedas).</param>
    /// <param name="dataDesembolso">Data de desembolso (início do fluxo).</param>
    /// <param name="taxaAaPercentualOverride">Substitui a taxa da proposta quando informado.</param>
    /// <returns>CET em % a.a. (ex: 8.42m para 8,42%).</returns>
    public static decimal CalcularCetLei4131(
        Proposta proposta,
        decimal ptaxUsdBrl,
        LocalDate dataDesembolso,
        decimal? taxaAaPercentualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        // Guard: Lei 4131 exige moeda estrangeira — invariante SPEC §7.1 e §4.2.
        if (proposta.MoedaOriginal == Moeda.Brl)
        {
            throw new ArgumentException(
                $"CalcularCetLei4131 exige moeda estrangeira (recebido: Brl). " +
                "Lei 4131 não suporta operações domésticas em BRL.",
                nameof(proposta));
        }

        // Delega a CalcularCetFinimp: fórmulas idênticas — SPEC §7.3 e §7.5.
        return CalcularCetFinimp(proposta, ptaxUsdBrl, dataDesembolso, taxaAaPercentualOverride);
    }

    /// <summary>
    /// Calcula o CET anualizado em percentual (ex: 14.5 para 14,5% a.a.)
    /// para propostas da modalidade NCE (Nota de Crédito à Exportação).
    /// <para>
    /// NCE é operação doméstica em BRL: sem IRRF (isenção lei 6.313/1975),
    /// sem IOF câmbio (não há conversão cambial), sem NDF. O IOF crédito
    /// (alíquota interna) é custo em t=0 que compõe o CET. Base 360 dias.
    /// </para>
    /// <para>
    /// Fórmula (SPEC §7.3): fluxo BRL → TIR Newton-Raphson → anualização base 360.
    /// </para>
    /// Onda 2 — SPEC §7 (docs/specs/cotacoes/modalidades/nce.md).
    /// </summary>
    /// <param name="proposta">Proposta NCE com MoedaOriginal=Brl e ExigeNdf=false.</param>
    /// <param name="dataDesembolso">Data de desembolso (início do fluxo).</param>
    /// <param name="taxaAaPercentualOverride">
    /// Substitui a taxa da proposta quando informado. Usado para CET do contrato
    /// fechado com taxa final negociada (SPEC §5.2 — mesmo padrão do FINIMP).
    /// </param>
    /// <returns>CET em % a.a. (ex: 14.5m para 14,5%).</returns>
    public static decimal CalcularCetNce(
        Proposta proposta,
        LocalDate dataDesembolso,
        decimal? taxaAaPercentualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        // Guard: NCE exige BRL — invariante SPEC §7.1 e §10.3.
        if (proposta.MoedaOriginal != Moeda.Brl)
        {
            throw new ArgumentException(
                $"CalcularCetNce exige MoedaOriginal=Brl (recebido: {proposta.MoedaOriginal}). " +
                "NCE não tem conversão cambial.",
                nameof(proposta));
        }

        // Guard: NCE não aceita NDF — invariante SPEC §7.1 e §10.3.
        if (proposta.ExigeNdf)
        {
            throw new ArgumentException(
                "CalcularCetNce não aceita ExigeNdf=true. NCE é operação em BRL sem hedge cambial.",
                nameof(proposta));
        }

        // O principal já está em BRL — ConverterParaBrl é no-op para Moeda.Brl.
        // Passamos ptax=1m como sentinel para reutilizar MontarFluxoBrl sem alterar o cálculo.
        const decimal PtaxBrlSentinel = 1m;
        Money valorBrl = proposta.ValorOferecidoMoedaOriginal;

        LocalDate dataVencimento = dataDesembolso.PlusDays(proposta.PrazoDias);
        decimal taxaBase = taxaAaPercentualOverride ?? proposta.TaxaAaPercentualDecimal;
        decimal taxaEfetiva = taxaBase + proposta.SpreadAaPercentualDecimal;

        IReadOnlyList<EventoCronogramaGerado> eventos = ProjetarFluxo(
            proposta,
            valorBrl,
            taxaEfetiva,
            dataDesembolso,
            dataVencimento);

        // MontarFluxoBrl aplica IOF crédito em t=0 — mesmo mecanismo do FINIMP.
        // Para NCE: guard acima impede ExigeNdf=true (sem custo NDF no fluxo).
        // Rendimento CDB cativo é suportado se banco exigir (proposta.GarantiaEhCdbCativo).
        // Sem IRRF e sem IOF câmbio — invisíveis nesta implementação pois a fórmula
        // nunca os incluiu; o guard acima é a barreira formal (SPEC §2.3 e EC-15).
        List<(int DiasFromT0, decimal FluxoBrl)> fluxos = MontarFluxoBrl(
            eventos,
            dataDesembolso,
            valorBrl,
            proposta,
            PtaxBrlSentinel);

        decimal tirDiaria = CalcularTirDiaria(fluxos);
        decimal cetAa = AnualizarTaxaDiaria(tirDiaria, proposta.PrazoDias);

        // CET tem floor em 0% — mesmo comportamento do FINIMP (SPEC §5.1).
        decimal cetAjustado = Math.Max(0m, cetAa);

        return Math.Round(cetAjustado * 100m, 6, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calcula o CET anualizado em percentual (ex: 14.5 para 14,5% a.a.)
    /// para propostas da modalidade Capital de Giro.
    /// <para>
    /// Capital de Giro é operação doméstica em BRL: sem PTAX, sem NDF, sem IRRF.
    /// IOF crédito (alíquota interna) é custo em t=0 que compõe o CET. Base 360 dias.
    /// </para>
    /// <para>
    /// A fórmula é idêntica a <see cref="CalcularCetNce"/>: fluxo BRL → TIR Newton-Raphson → anualização.
    /// A diferença é que Capital de Giro admite qualquer banco (não restrito a emissão de nota específica).
    /// </para>
    /// Onda 3b — SPEC §7 (docs/specs/cotacoes/modalidades/capital-de-giro.md).
    /// </summary>
    /// <param name="proposta">Proposta Capital de Giro com MoedaOriginal=Brl e ExigeNdf=false.</param>
    /// <param name="dataDesembolso">Data de desembolso (início do fluxo).</param>
    /// <param name="taxaAaPercentualOverride">
    /// Substitui a taxa da proposta quando informado. Usado para CET do contrato
    /// fechado com taxa final negociada (SPEC §5.2 — mesmo padrão do FINIMP e NCE).
    /// </param>
    /// <returns>CET em % a.a. (ex: 14.5m para 14,5%).</returns>
    public static decimal CalcularCetCapitalDeGiro(
        Proposta proposta,
        LocalDate dataDesembolso,
        decimal? taxaAaPercentualOverride = null)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        // Guard: Capital de Giro exige BRL — operação doméstica sem conversão cambial (SPEC §3.2).
        if (proposta.MoedaOriginal != Moeda.Brl)
        {
            throw new ArgumentException(
                $"CalcularCetCapitalDeGiro exige MoedaOriginal=Brl (recebido: {proposta.MoedaOriginal}). " +
                "Capital de Giro é operação doméstica em BRL.",
                nameof(proposta));
        }

        // Guard: Capital de Giro não aceita NDF — sem exposição cambial (SPEC §3.2 e EC-2).
        if (proposta.ExigeNdf)
        {
            throw new ArgumentException(
                "CalcularCetCapitalDeGiro não aceita ExigeNdf=true. " +
                "Capital de Giro é operação em BRL sem hedge cambial.",
                nameof(proposta));
        }

        // Reutiliza o motor de CalcularCetNce: fórmula BRL pura idêntica.
        // Diferença de produto (NCE vs Capital de Giro) não afeta a matemática do CET;
        // a distinção é de negócio (finalidade declarada e tipo de documento).
        return CalcularCetNce(proposta, dataDesembolso, taxaAaPercentualOverride);
    }

    /// <summary>
    /// Stub: cálculo do CET para FGI (Fundo Garantidor de Investimentos).
    /// Implementação pendente — veja docs/specs/cotacoes/modalidades/fgi.md.
    /// Onda 0 F0.2.
    /// </summary>
    public static decimal CalcularCetFgi(
        Proposta proposta,
        LocalDate dataDesembolso,
        FgiInputs fgiInputs,
        decimal? taxaAaPercentualOverride = null) =>
        throw new NotImplementedException(
            "Implementação pendente — Onda 3. Veja docs/specs/cotacoes/modalidades/fgi.md.");

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

using System.Collections.Frozen;

using NodaTime;
using NodaTime.TimeZones;

using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Simulacao;

/// <summary>
/// Child entity do agregado <see cref="CenarioSimulacao"/>.
/// Representa uma captação hipotética futura com todos os campos necessários
/// para gerar um cronograma idêntico ao que um contrato real produziria.
///
/// Invariantes (SPEC §6.3):
///   I-1  ValorPrincipal &gt; 0
///   I-2  DataContratacaoPrevista &gt;= hoje (clock)
///   I-3  DataPrimeiroVencimento &gt; DataContratacaoPrevista
///   I-4  DataContratacaoPrevista dentro de [anoBase-01-01, anoBase-12-31]
///   I-5  QuantidadeParcelas &gt;= 1
///   I-6  TipoTaxa.Fixa  → TaxaAa != null, SpreadAa == null
///   I-7  TipoTaxa.CdiSpread → SpreadAa != null, TaxaAa == null, Moeda == Brl
///   I-8  Modalidades cambiais (Finimp, Lei4131) não aceitam Brl
///   I-9  BancoId válido (verificado na Application antes de invocar)
///   I-10 Combinação EstruturaAmortizacao + Periodicidade válida (verificado no calculator)
///   I-11 GarantiaExigidaPrevista &lt;= 500 caracteres quando informada
///
/// AD-3: Version incrementado em cada mutação para invalidar cache Redis.
/// </summary>
public sealed class SimulacaoContratacao : Entity
{
    // ── Fuso horário brasileiro (Brasília) ────────────────────────────────────
    // Datas de calendário (vencimento, contratação prevista) representam dias
    // úteis brasileiros e devem ser derivadas a partir do horário local BRT,
    // não de UTC. Entre 21h e 23:59 BRT, a data UTC já avançou para o dia
    // seguinte, mas o calendário BR ainda está no dia anterior.
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    // ── Modalidades que exigem moeda estrangeira (não aceitam BRL) ────────────

    private static readonly FrozenSet<ModalidadeContrato> ModalidadesCambiais =
        new HashSet<ModalidadeContrato>
        {
            ModalidadeContrato.Finimp,
            ModalidadeContrato.Lei4131
        }.ToFrozenSet();

    // ── Propriedades ──────────────────────────────────────────────────────────

    public Guid CenarioId { get; private set; }
    public Guid BancoId { get; private set; }
    public ModalidadeContrato Modalidade { get; private set; }
    public Moeda Moeda { get; private set; }

    // Money é stored em duas colunas pela configuração EF: Valor + Moeda
    internal decimal ValorPrincipalDecimal { get; private set; }
    internal int ValorPrincipalMoedaInt { get; private set; }

    /// <summary>Valor principal da operação na moeda informada.</summary>
    public Money ValorPrincipal => new(ValorPrincipalDecimal, (Moeda)ValorPrincipalMoedaInt);

    public LocalDate DataContratacaoPrevista { get; private set; }
    public LocalDate DataPrimeiroVencimento { get; private set; }

    public TipoTaxa TipoTaxa { get; private set; }

    /// <summary>Taxa nominal anual. Preenchida quando <see cref="TipoTaxa"/> = Fixa.</summary>
    public Percentual? TaxaAa { get; private set; }

    /// <summary>Spread anual sobre CDI. Preenchido quando <see cref="TipoTaxa"/> = CdiSpread.</summary>
    public Percentual? SpreadAa { get; private set; }

    public BaseCalculo BaseCalculo { get; private set; }
    public EstruturaAmortizacao EstruturaAmortizacao { get; private set; }
    public Periodicidade Periodicidade { get; private set; }
    public int QuantidadeParcelas { get; private set; }
    public AnchorDiaMes AnchorDiaMes { get; private set; }

    /// <summary>Dia fixo do mês (1-31). Relevante quando <see cref="AnchorDiaMes"/> = DiaFixo.</summary>
    public int? AnchorDiaFixo { get; private set; }

    /// <summary>
    /// Garantia prevista — campo livre informativo (SPEC D-9 / I-11).
    /// Máx. 500 caracteres. Não validado contra LimiteBanco.
    /// </summary>
    public string? GarantiaExigidaPrevista { get; private set; }

    public string? Observacoes { get; private set; }

    /// <summary>
    /// Versão da simulação — incrementada em cada mutação.
    /// Usada como chave de invalidação do cache Redis (AD-3).
    /// </summary>
    public int Version { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    // ── Construtor privado para EF Core ──────────────────────────────────────

    private SimulacaoContratacao() { }

    // ── Factory principal ─────────────────────────────────────────────────────

    /// <summary>
    /// Cria uma nova simulação de contratação, validando todos os invariantes I-1..I-11.
    /// </summary>
    /// <param name="anoBase">
    /// Ano base do cenário pai. Quando informado, valida I-4 (data dentro do ano).
    /// Se null, ignora a validação de ano (para uso em duplicação onde a data já foi validada).
    /// </param>
    public static SimulacaoContratacao Criar(
        Guid cenarioId,
        Guid bancoId,
        ModalidadeContrato modalidade,
        Moeda moeda,
        Money valorPrincipal,
        LocalDate dataContratacaoPrevista,
        LocalDate dataPrimeiroVencimento,
        TipoTaxa tipoTaxa,
        Percentual? taxaAa,
        Percentual? spreadAa,
        BaseCalculo baseCalculo,
        EstruturaAmortizacao estruturaAmortizacao,
        Periodicidade periodicidade,
        int quantidadeParcelas,
        AnchorDiaMes anchorDiaMes,
        int? anchorDiaFixo,
        string? garantiaExigidaPrevista,
        string? observacoes,
        IClock clock,
        int? anoBase = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        Instant agora = clock.GetCurrentInstant();
        // Datas de calendário brasileiras derivam do fuso BRT, não UTC.
        // Entre 21h–23:59 BRT, a data UTC já é o dia seguinte — usar UTC
        // rejeitaria indevidamente "dataContratacaoPrevista = hoje BRT".
        LocalDate hoje = agora.InZone(FusoBrasilia).Date;

        // I-1: ValorPrincipal > 0
        if (valorPrincipal.Valor <= 0m)
        {
            throw new ArgumentException("ValorPrincipal (principal) deve ser maior que zero.", nameof(valorPrincipal));
        }

        // I-2: DataContratacaoPrevista >= hoje
        if (dataContratacaoPrevista < hoje)
        {
            throw new ArgumentException(
                $"DataContratacaoPrevista (contratacao) não pode ser no passado. Hoje: {hoje}. Informada: {dataContratacaoPrevista}.",
                nameof(dataContratacaoPrevista));
        }

        // I-3: DataPrimeiroVencimento > DataContratacaoPrevista
        if (dataPrimeiroVencimento <= dataContratacaoPrevista)
        {
            throw new ArgumentException(
                "DataPrimeiroVencimento (vencimento) deve ser posterior à DataContratacaoPrevista.",
                nameof(dataPrimeiroVencimento));
        }

        // I-4: DataContratacaoPrevista dentro do anoBase (quando informado)
        if (anoBase.HasValue)
        {
            var inicioAno = new LocalDate(anoBase.Value, 1, 1);
            var fimAno = new LocalDate(anoBase.Value, 12, 31);
            if (dataContratacaoPrevista < inicioAno || dataContratacaoPrevista > fimAno)
            {
                throw new ArgumentException(
                    $"DataContratacaoPrevista deve estar dentro do ano base {anoBase.Value} " +
                    $"([{inicioAno}, {fimAno}]). Informada: {dataContratacaoPrevista}.",
                    nameof(dataContratacaoPrevista));
            }
        }

        // I-5: QuantidadeParcelas >= 1
        if (quantidadeParcelas < 1)
        {
            throw new ArgumentException(
                "QuantidadeParcelas (parcelas) deve ser no mínimo 1.",
                nameof(quantidadeParcelas));
        }

        // I-6: TipoTaxa.Fixa exige TaxaAa, proíbe SpreadAa
        if (tipoTaxa == TipoTaxa.Fixa)
        {
            if (!taxaAa.HasValue)
            {
                throw new ArgumentException(
                    "TipoTaxa Fixa exige que TaxaAa (taxa) seja informada.",
                    nameof(taxaAa));
            }

            if (spreadAa.HasValue)
            {
                throw new ArgumentException(
                    "TipoTaxa Fixa não aceita SpreadAa (spread). Use TipoTaxa.CdiSpread para operações indexadas ao CDI.",
                    nameof(spreadAa));
            }
        }

        // I-7: TipoTaxa.CdiSpread exige SpreadAa, proíbe TaxaAa, exige Moeda == Brl
        if (tipoTaxa == TipoTaxa.CdiSpread)
        {
            if (!spreadAa.HasValue)
            {
                throw new ArgumentException(
                    "TipoTaxa CdiSpread exige que SpreadAa (spread) seja informado.",
                    nameof(spreadAa));
            }

            if (moeda != Moeda.Brl)
            {
                throw new ArgumentException(
                    $"TipoTaxa CdiSpread (CDI) só é válido para operações em BRL. Moeda informada: {moeda}.",
                    nameof(moeda));
            }
        }

        // I-8: Modalidades cambiais não aceitam BRL
        if (ModalidadesCambiais.Contains(modalidade) && moeda == Moeda.Brl)
        {
            throw new ArgumentException(
                $"Modalidade {modalidade} (modalidade) não aceita operações em BRL. " +
                "Use uma moeda estrangeira (USD, EUR, JPY, CNY).",
                nameof(modalidade));
        }

        // I-11: GarantiaExigidaPrevista <= 500 chars
        if (garantiaExigidaPrevista is { Length: > 500 })
        {
            throw new ArgumentException(
                "GarantiaExigidaPrevista (garantia) não pode exceder 500 caracteres.",
                nameof(garantiaExigidaPrevista));
        }

        return new SimulacaoContratacao
        {
            CenarioId = cenarioId,
            BancoId = bancoId,
            Modalidade = modalidade,
            Moeda = moeda,
            ValorPrincipalDecimal = valorPrincipal.Valor,
            ValorPrincipalMoedaInt = (int)moeda,
            DataContratacaoPrevista = dataContratacaoPrevista,
            DataPrimeiroVencimento = dataPrimeiroVencimento,
            TipoTaxa = tipoTaxa,
            TaxaAa = taxaAa,
            SpreadAa = spreadAa,
            BaseCalculo = baseCalculo,
            EstruturaAmortizacao = estruturaAmortizacao,
            Periodicidade = periodicidade,
            QuantidadeParcelas = quantidadeParcelas,
            AnchorDiaMes = anchorDiaMes,
            AnchorDiaFixo = anchorDiaFixo,
            GarantiaExigidaPrevista = garantiaExigidaPrevista,
            Observacoes = observacoes,
            Version = 1,
            CreatedAt = agora,
            UpdatedAt = agora
        };
    }

    // ── Mutação com incremento de Version ────────────────────────────────────

    /// <summary>
    /// Atualiza os campos da simulação, validando os invariantes e incrementando Version (AD-3).
    /// </summary>
    public void Atualizar(
        Money valorPrincipal,
        LocalDate dataContratacaoPrevista,
        LocalDate dataPrimeiroVencimento,
        TipoTaxa tipoTaxa,
        Percentual? taxaAa,
        Percentual? spreadAa,
        BaseCalculo baseCalculo,
        EstruturaAmortizacao estruturaAmortizacao,
        Periodicidade periodicidade,
        int quantidadeParcelas,
        AnchorDiaMes anchorDiaMes,
        int? anchorDiaFixo,
        string? garantiaExigidaPrevista,
        string? observacoes,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (valorPrincipal.Valor <= 0m)
        {
            throw new ArgumentException("ValorPrincipal (principal) deve ser maior que zero.", nameof(valorPrincipal));
        }

        if (dataPrimeiroVencimento <= dataContratacaoPrevista)
        {
            throw new ArgumentException(
                "DataPrimeiroVencimento (vencimento) deve ser posterior à DataContratacaoPrevista.",
                nameof(dataPrimeiroVencimento));
        }

        if (quantidadeParcelas < 1)
        {
            throw new ArgumentException("QuantidadeParcelas (parcelas) deve ser no mínimo 1.", nameof(quantidadeParcelas));
        }

        if (tipoTaxa == TipoTaxa.Fixa && !taxaAa.HasValue)
        {
            throw new ArgumentException("TipoTaxa Fixa exige que TaxaAa (taxa) seja informada.", nameof(taxaAa));
        }

        if (tipoTaxa == TipoTaxa.CdiSpread && !spreadAa.HasValue)
        {
            throw new ArgumentException("TipoTaxa CdiSpread exige que SpreadAa (spread) seja informado.", nameof(spreadAa));
        }

        if (garantiaExigidaPrevista is { Length: > 500 })
        {
            throw new ArgumentException("GarantiaExigidaPrevista (garantia) não pode exceder 500 caracteres.", nameof(garantiaExigidaPrevista));
        }

        ValorPrincipalDecimal = valorPrincipal.Valor;
        DataContratacaoPrevista = dataContratacaoPrevista;
        DataPrimeiroVencimento = dataPrimeiroVencimento;
        TipoTaxa = tipoTaxa;
        TaxaAa = taxaAa;
        SpreadAa = spreadAa;
        BaseCalculo = baseCalculo;
        EstruturaAmortizacao = estruturaAmortizacao;
        Periodicidade = periodicidade;
        QuantidadeParcelas = quantidadeParcelas;
        AnchorDiaMes = anchorDiaMes;
        AnchorDiaFixo = anchorDiaFixo;
        GarantiaExigidaPrevista = garantiaExigidaPrevista;
        Observacoes = observacoes;

        // AD-3: incrementa Version para invalidar cache Redis
        Version++;
        UpdatedAt = clock.GetCurrentInstant();
    }

    // ── Factory interna para cópia profunda (usada por DuplicarComoRascunho) ──

    /// <summary>
    /// Cria uma cópia desta simulação com novo Id e vinculada a outro cenário.
    /// Version é resetada para 1 (nova entidade, cache do original não interfere).
    /// </summary>
    internal SimulacaoContratacao CopiarParaCenario(Guid novoCenarioId, Instant agora) =>
        new()
        {
            CenarioId = novoCenarioId,
            BancoId = BancoId,
            Modalidade = Modalidade,
            Moeda = Moeda,
            ValorPrincipalDecimal = ValorPrincipalDecimal,
            ValorPrincipalMoedaInt = ValorPrincipalMoedaInt,
            DataContratacaoPrevista = DataContratacaoPrevista,
            DataPrimeiroVencimento = DataPrimeiroVencimento,
            TipoTaxa = TipoTaxa,
            TaxaAa = TaxaAa,
            SpreadAa = SpreadAa,
            BaseCalculo = BaseCalculo,
            EstruturaAmortizacao = EstruturaAmortizacao,
            Periodicidade = Periodicidade,
            QuantidadeParcelas = QuantidadeParcelas,
            AnchorDiaMes = AnchorDiaMes,
            AnchorDiaFixo = AnchorDiaFixo,
            GarantiaExigidaPrevista = GarantiaExigidaPrevista,
            Observacoes = Observacoes,
            Version = 1,    // cache da cópia começa limpo
            CreatedAt = agora,
            UpdatedAt = agora
        };
}

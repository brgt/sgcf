using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Calendario;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Contratos;

public sealed class Contrato : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public string NumeroExterno { get; private set; } = default!;
    public string? CodigoInterno { get; private set; }
    public Guid BancoId { get; private set; }
    public ModalidadeContrato Modalidade { get; private set; }
    public Moeda Moeda { get; private set; }

    internal decimal ValorPrincipalDecimal { get; private set; }
    public Money ValorPrincipal => new(ValorPrincipalDecimal, Moeda);

    public LocalDate DataContratacao { get; private set; }
    public LocalDate DataVencimento { get; private set; }

    internal decimal TaxaAaDecimal { get; private set; }
    public Percentual TaxaAa => Percentual.DeFracao(TaxaAaDecimal);

    public BaseCalculo BaseCalculo { get; private set; }
    public StatusContrato Status { get; private set; }
    public Guid? ContratoPaiId { get; private set; }
    public string? Observacoes { get; private set; }

    public Periodicidade Periodicidade { get; private set; }
    public EstruturaAmortizacao EstruturaAmortizacao { get; private set; }
    public LocalDate DataPrimeiroVencimento { get; private set; }
    public int QuantidadeParcelas { get; private set; }
    public AnchorDiaMes AnchorDiaMes { get; private set; }
    public int? AnchorDiaFixo { get; private set; }
    public Periodicidade? PeriodicidadeJuros { get; private set; }
    public ConvencaoDataNaoUtil ConvencaoDataNaoUtil { get; private set; }

    // ── Política do banco no momento da contratação (SC-05 — imutáveis após preenchimento) ──

    /// <summary>
    /// Identificador do <c>LimiteBanco</c> vigente no momento da conversão cotação→contrato.
    /// Nulo para contratos pré-feature ou criados sem <c>LimiteBanco</c> cadastrado (SC-06/SC-07).
    /// </summary>
    public Guid? LimiteBancoId { get; private set; }

    /// <summary>
    /// Identificador do <c>LimiteGlobalBanco</c> vigente no momento da conversão.
    /// Nulo quando não há limite global para o banco (SC-06/SC-07).
    /// </summary>
    public Guid? LimiteGlobalBancoId { get; private set; }

    /// <summary>
    /// Identificador da <c>GarantiaExigidaRevisao</c> vigente no momento da conversão.
    /// Nulo quando o banco não tem política de garantias formalizada (SC-06/SC-07).
    /// </summary>
    public Guid? GarantiasExigidasRevisaoId { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Instant? DeletedAt { get; private set; }

    private readonly List<Parcela> _parcelas = [];
    private readonly List<Garantia> _garantias = [];
    private readonly List<EventoCronograma> _eventosCronograma = [];

    public IReadOnlyCollection<Parcela> Parcelas => _parcelas.AsReadOnly();
    public IReadOnlyCollection<Garantia> Garantias => _garantias.AsReadOnly();
    public IReadOnlyCollection<EventoCronograma> EventosCronograma => _eventosCronograma.AsReadOnly();

    private Contrato() { }

    public static Contrato Criar(
        string numeroExterno,
        Guid bancoId,
        ModalidadeContrato modalidade,
        Money valorPrincipal,
        LocalDate dataContratacao,
        LocalDate dataVencimento,
        Percentual taxaAa,
        BaseCalculo baseCalculo,
        IClock clock,
        Periodicidade periodicidade = Periodicidade.Bullet,
        EstruturaAmortizacao estruturaAmortizacao = EstruturaAmortizacao.Bullet,
        int quantidadeParcelas = 1,
        LocalDate? dataPrimeiroVencimento = null,
        AnchorDiaMes anchorDiaMes = AnchorDiaMes.DiaContratacao,
        int? anchorDiaFixo = null,
        Periodicidade? periodicidadeJuros = null,
        ConvencaoDataNaoUtil convencaoDataNaoUtil = ConvencaoDataNaoUtil.Following,
        Guid? contratoPaiId = null,
        string? observacoes = null)
    {
        if (string.IsNullOrWhiteSpace(numeroExterno))
        {
            throw new ArgumentException("NumeroExterno não pode ser vazio.", nameof(numeroExterno));
        }

        if (dataVencimento <= dataContratacao)
        {
            throw new ArgumentException("DataVencimento deve ser posterior a DataContratacao.", nameof(dataVencimento));
        }

        if (quantidadeParcelas < 1)
        {
            throw new ArgumentException("QuantidadeParcelas deve ser maior ou igual a 1.", nameof(quantidadeParcelas));
        }

        LocalDate primeiroVencimento = dataPrimeiroVencimento ?? dataVencimento;

        if (primeiroVencimento <= dataContratacao)
        {
            throw new ArgumentException("DataPrimeiroVencimento deve ser posterior a DataContratacao.", nameof(dataPrimeiroVencimento));
        }

        if (anchorDiaMes == AnchorDiaMes.DiaFixo && anchorDiaFixo is null)
        {
            throw new ArgumentException("AnchorDiaFixo é obrigatório quando AnchorDiaMes é DiaFixo.", nameof(anchorDiaFixo));
        }

        if (anchorDiaMes == AnchorDiaMes.DiaFixo && anchorDiaFixo is < 1 or > 31)
        {
            throw new ArgumentException("AnchorDiaFixo deve estar entre 1 e 31.", nameof(anchorDiaFixo));
        }

        if (anchorDiaMes != AnchorDiaMes.DiaFixo && anchorDiaFixo is not null)
        {
            throw new ArgumentException("AnchorDiaFixo só pode ser informado quando AnchorDiaMes é DiaFixo.", nameof(anchorDiaFixo));
        }

        var now = clock.GetCurrentInstant();
        return new Contrato
        {
            NumeroExterno = numeroExterno,
            BancoId = bancoId,
            Modalidade = modalidade,
            Moeda = valorPrincipal.Moeda,
            ValorPrincipalDecimal = valorPrincipal.Valor,
            DataContratacao = dataContratacao,
            DataVencimento = dataVencimento,
            TaxaAaDecimal = taxaAa.AsDecimal,
            BaseCalculo = baseCalculo,
            Status = StatusContrato.Ativo,
            Periodicidade = periodicidade,
            EstruturaAmortizacao = estruturaAmortizacao,
            DataPrimeiroVencimento = primeiroVencimento,
            QuantidadeParcelas = quantidadeParcelas,
            AnchorDiaMes = anchorDiaMes,
            AnchorDiaFixo = anchorDiaFixo,
            PeriodicidadeJuros = periodicidadeJuros,
            ConvencaoDataNaoUtil = convencaoDataNaoUtil,
            ContratoPaiId = contratoPaiId,
            Observacoes = observacoes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AdicionarParcela(
        short numero,
        LocalDate dataVencimento,
        Money valorPrincipal,
        Money valorJuros)
    {
        if (valorPrincipal.Moeda != Moeda)
        {
            throw new ArgumentException("Moeda da parcela não corresponde à moeda do contrato.", nameof(valorPrincipal));
        }

        if (valorJuros.Moeda != Moeda)
        {
            throw new ArgumentException("Moeda dos juros não corresponde à moeda do contrato.", nameof(valorJuros));
        }

        _parcelas.Add(Parcela.Criar(Id, numero, dataVencimento, valorPrincipal, valorJuros));
    }

    public void AdicionarEventoCronograma(EventoCronograma evento)
    {
        _eventosCronograma.Add(evento);
    }

    public void SetCodigoInterno(string codigo)
    {
        CodigoInterno = codigo;
    }

    public void Liquidar(IClock clock)
    {
        Status = StatusContrato.Liquidado;
        UpdatedAt = clock.GetCurrentInstant();
    }

    public void MarcarVencido(IClock clock)
    {
        Status = StatusContrato.Vencido;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Marca este contrato como parcialmente refinanciado — menos de 100% do principal foi refinanciado.
    /// Chamado quando um REFINIMP é criado referenciando este contrato e cobre menos de 100%.
    /// </summary>
    public void MarcarRefinanciadoParcial(IClock clock)
    {
        Status = StatusContrato.RefinanciadoParcial;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Marca este contrato como totalmente refinanciado — 100% ou mais do principal foi refinanciado.
    /// Chamado quando um REFINIMP é criado referenciando este contrato e cobre 100%.
    /// </summary>
    public void MarcarRefinanciadoTotal(IClock clock)
    {
        Status = StatusContrato.RefinanciadoTotal;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Atualiza os campos mutáveis do contrato após criação.
    /// Apenas os parâmetros não-nulos são aplicados; os demais permanecem inalterados.
    /// </summary>
    public void Atualizar(
        IClock clock,
        string? numeroExterno = null,
        Percentual? taxaAa = null,
        LocalDate? dataVencimento = null,
        string? observacoes = null,
        BaseCalculo? baseCalculo = null,
        Periodicidade? periodicidade = null,
        EstruturaAmortizacao? estruturaAmortizacao = null,
        int? quantidadeParcelas = null,
        LocalDate? dataPrimeiroVencimento = null,
        ConvencaoDataNaoUtil? convencaoDataNaoUtil = null)
    {
        if (dataVencimento.HasValue && dataVencimento.Value <= DataContratacao)
        {
            throw new ArgumentException("DataVencimento deve ser posterior a DataContratacao.", nameof(dataVencimento));
        }

        if (dataPrimeiroVencimento.HasValue && dataPrimeiroVencimento.Value <= DataContratacao)
        {
            throw new ArgumentException("DataPrimeiroVencimento deve ser posterior a DataContratacao.", nameof(dataPrimeiroVencimento));
        }

        if (quantidadeParcelas.HasValue && quantidadeParcelas.Value < 1)
        {
            throw new ArgumentException("QuantidadeParcelas deve ser maior ou igual a 1.", nameof(quantidadeParcelas));
        }

        if (numeroExterno is not null)
        {
            NumeroExterno = numeroExterno;
        }

        if (taxaAa is not null)
        {
            TaxaAaDecimal = taxaAa.Value.AsDecimal;
        }

        if (dataVencimento.HasValue)
        {
            DataVencimento = dataVencimento.Value;
        }

        if (observacoes is not null)
        {
            Observacoes = observacoes;
        }

        if (baseCalculo.HasValue)
        {
            BaseCalculo = baseCalculo.Value;
        }

        if (periodicidade.HasValue)
        {
            Periodicidade = periodicidade.Value;
        }

        if (estruturaAmortizacao.HasValue)
        {
            EstruturaAmortizacao = estruturaAmortizacao.Value;
        }

        if (quantidadeParcelas.HasValue)
        {
            QuantidadeParcelas = quantidadeParcelas.Value;
        }

        if (dataPrimeiroVencimento.HasValue)
        {
            DataPrimeiroVencimento = dataPrimeiroVencimento.Value;
        }

        if (convencaoDataNaoUtil.HasValue)
        {
            ConvencaoDataNaoUtil = convencaoDataNaoUtil.Value;
        }

        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Vincula o contrato à política vigente do banco no momento da conversão cotação→contrato.
    /// Idempotente: re-chamada com exatamente os mesmos valores (incluindo combinações de nulos)
    /// é no-op silencioso — necessário para retries de transação.
    /// Lança <see cref="InvalidOperationException"/> se qualquer campo já preenchido receber
    /// um valor diferente — snapshot imutável (SPEC §3.5, invariante SC-05).
    /// Não atualiza <c>UpdatedAt</c>: vinculação é metadado de criação, não modificação do contrato.
    /// </summary>
    internal void VincularPoliticaBanco(
        Guid? limiteBancoId,
        Guid? limiteGlobalBancoId,
        Guid? garantiasExigidasRevisaoId)
    {
        // Idempotência: mesma combinação de valores (inclusive todos nulos) → no-op.
        if (LimiteBancoId == limiteBancoId
            && LimiteGlobalBancoId == limiteGlobalBancoId
            && GarantiasExigidasRevisaoId == garantiasExigidasRevisaoId)
        {
            return;
        }

        // Imutabilidade: campo já preenchido não pode receber valor diferente.
        if (LimiteBancoId is not null && LimiteBancoId != limiteBancoId)
        {
            throw new InvalidOperationException(
                $"LimiteBancoId já está vinculado a '{LimiteBancoId}' e não pode ser alterado para '{limiteBancoId}'.");
        }

        if (LimiteGlobalBancoId is not null && LimiteGlobalBancoId != limiteGlobalBancoId)
        {
            throw new InvalidOperationException(
                $"LimiteGlobalBancoId já está vinculado a '{LimiteGlobalBancoId}' e não pode ser alterado para '{limiteGlobalBancoId}'.");
        }

        if (GarantiasExigidasRevisaoId is not null && GarantiasExigidasRevisaoId != garantiasExigidasRevisaoId)
        {
            throw new InvalidOperationException(
                $"GarantiasExigidasRevisaoId já está vinculado a '{GarantiasExigidasRevisaoId}' e não pode ser alterado para '{garantiasExigidasRevisaoId}'.");
        }

        LimiteBancoId = limiteBancoId;
        LimiteGlobalBancoId = limiteGlobalBancoId;
        GarantiasExigidasRevisaoId = garantiasExigidasRevisaoId;
    }

    public void Deletar(IClock clock) => DeletedAt = clock.GetCurrentInstant();
}

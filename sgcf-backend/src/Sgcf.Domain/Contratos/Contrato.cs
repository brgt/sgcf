using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Domain.Contratos;

public sealed class Contrato : Entity
{
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

    public void Deletar(IClock clock) => DeletedAt = clock.GetCurrentInstant();
}

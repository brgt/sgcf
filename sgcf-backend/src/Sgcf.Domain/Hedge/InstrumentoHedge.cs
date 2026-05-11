using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Hedge;

public sealed class InstrumentoHedge : Entity
{
    public Guid ContratoId { get; private set; }
    public TipoHedge Tipo { get; private set; }
    public Guid ContraparteId { get; private set; }

    internal decimal NotionalDecimal { get; private set; }

    public Moeda MoedaBase { get; private set; }
    public Moeda MoedaQuote { get; private set; }
    public Money Notional => new(NotionalDecimal, MoedaBase);

    public LocalDate DataContratacao { get; private set; }
    public LocalDate DataVencimento { get; private set; }

    public decimal? StrikeForward { get; private set; }
    public decimal? StrikePut { get; private set; }
    public decimal? StrikeCall { get; private set; }

    public StatusHedge Status { get; private set; }

    public Instant CreatedAt { get; private set; }

    private InstrumentoHedge() { }

    public static InstrumentoHedge CriarForward(
        Guid contratoId,
        Guid contraparteId,
        Money notional,
        LocalDate dataCont,
        LocalDate dataVenc,
        decimal strikeForward,
        IClock clock)
    {
        if (dataVenc <= dataCont)
        {
            throw new ArgumentException("DataVencimento deve ser posterior a DataContratacao.", nameof(dataVenc));
        }

        if (strikeForward <= 0)
        {
            throw new ArgumentException("StrikeForward deve ser positivo.", nameof(strikeForward));
        }

        return new InstrumentoHedge
        {
            ContratoId = contratoId,
            Tipo = TipoHedge.NdfForward,
            ContraparteId = contraparteId,
            NotionalDecimal = notional.Valor,
            MoedaBase = notional.Moeda,
            MoedaQuote = Moeda.Brl,
            DataContratacao = dataCont,
            DataVencimento = dataVenc,
            StrikeForward = strikeForward,
            Status = StatusHedge.Ativo,
            CreatedAt = clock.GetCurrentInstant()
        };
    }

    public static InstrumentoHedge CriarCollar(
        Guid contratoId,
        Guid contraparteId,
        Money notional,
        LocalDate dataCont,
        LocalDate dataVenc,
        decimal strikePut,
        decimal strikeCall,
        IClock clock)
    {
        if (dataVenc <= dataCont)
        {
            throw new ArgumentException("DataVencimento deve ser posterior a DataContratacao.", nameof(dataVenc));
        }

        if (strikePut <= 0)
        {
            throw new ArgumentException("StrikePut deve ser positivo.", nameof(strikePut));
        }

        if (strikeCall <= strikePut)
        {
            throw new ArgumentException("StrikeCall deve ser maior que StrikePut.", nameof(strikeCall));
        }

        return new InstrumentoHedge
        {
            ContratoId = contratoId,
            Tipo = TipoHedge.NdfCollar,
            ContraparteId = contraparteId,
            NotionalDecimal = notional.Valor,
            MoedaBase = notional.Moeda,
            MoedaQuote = Moeda.Brl,
            DataContratacao = dataCont,
            DataVencimento = dataVenc,
            StrikePut = strikePut,
            StrikeCall = strikeCall,
            Status = StatusHedge.Ativo,
            CreatedAt = clock.GetCurrentInstant()
        };
    }

    public void Cancelar() => Status = StatusHedge.Cancelado;

    public void Liquidar() => Status = StatusHedge.Liquidado;
}

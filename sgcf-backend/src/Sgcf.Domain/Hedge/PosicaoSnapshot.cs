using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Hedge;

public sealed class PosicaoSnapshot : Entity
{
    public Guid HedgeId { get; private set; }
    public Guid ContratoId { get; private set; }
    internal decimal MtmBrlDecimal { get; private set; }
    public Money MtmBrl => new(MtmBrlDecimal, Moeda.Brl);
    internal decimal SpotUtilizadoDecimal { get; private set; }
    public decimal SpotUtilizado => SpotUtilizadoDecimal;
    public Instant CalculadoEm { get; private set; }
    public string TipoCotacao { get; private set; } = string.Empty;

    private PosicaoSnapshot() { }

    public static PosicaoSnapshot Criar(
        Guid hedgeId,
        Guid contratoId,
        decimal mtmBrl,
        decimal spotUtilizado,
        string tipoCotacao,
        IClock clock) => new()
        {
            HedgeId = hedgeId,
            ContratoId = contratoId,
            MtmBrlDecimal = Math.Round(mtmBrl, 6, MidpointRounding.AwayFromZero),
            SpotUtilizadoDecimal = spotUtilizado,
            TipoCotacao = tipoCotacao,
            CalculadoEm = clock.GetCurrentInstant()
        };

    /// <summary>
    /// Creates a snapshot using a pre-fetched <see cref="Instant"/>.
    /// Use this overload in jobs that fetch the clock once for consistency
    /// across a batch, rather than calling the clock per-entity.
    /// </summary>
    public static PosicaoSnapshot CriarComInstant(
        Guid hedgeId,
        Guid contratoId,
        decimal mtmBrl,
        decimal spotUtilizado,
        string tipoCotacao,
        Instant calculadoEm) => new()
        {
            HedgeId = hedgeId,
            ContratoId = contratoId,
            MtmBrlDecimal = Math.Round(mtmBrl, 6, MidpointRounding.AwayFromZero),
            SpotUtilizadoDecimal = spotUtilizado,
            TipoCotacao = tipoCotacao,
            CalculadoEm = calculadoEm
        };
}

using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Snapshot diário da taxa CDI cadastrado manualmente no MVP.
/// Usado pela <see cref="Sgcf.Domain.Cotacoes.CalculadoraEconomia"/> para equalização de economia por CDI.
/// SPEC §13 decisão 2 — integração ANBIMA futura.
/// </summary>
public sealed class CdiSnapshot : Entity
{
    /// <summary>Data de referência do CDI (dia útil).</summary>
    public LocalDate Data { get; private set; }

    /// <summary>Taxa CDI em % a.a. (ex: 10.75 para 10,75%).</summary>
    public decimal CdiAaPercentual { get; private set; }

    public Instant CreatedAt { get; private set; }

    private CdiSnapshot() { }

    public static CdiSnapshot Criar(LocalDate data, decimal cdiAaPercentual, IClock clock)
    {
        if (cdiAaPercentual <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cdiAaPercentual), "CdiAaPercentual deve ser positivo.");
        }

        return new CdiSnapshot
        {
            Data = data,
            CdiAaPercentual = cdiAaPercentual,
            CreatedAt = clock.GetCurrentInstant(),
        };
    }
}

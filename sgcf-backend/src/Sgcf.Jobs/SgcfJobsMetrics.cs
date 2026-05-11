using System.Diagnostics.Metrics;

namespace Sgcf.Jobs;

internal static class SgcfJobsMetrics
{
    private static readonly Meter Meter = new("Sgcf.Jobs", "1.0.0");

    /// <summary>Incremented each time a hedge MTM is recalculated successfully by the intraday job.</summary>
    internal static readonly Counter<int> MtmRecalculadoTotal =
        Meter.CreateCounter<int>(
            "sgcf_mtm_recalculado_total",
            description: "Número total de hedges com MTM recalculado pelo job intraday.");
}

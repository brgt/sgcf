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

    /// <summary>Incremented each time a maturity alert is created by AlertaVencimentoJob.</summary>
    internal static readonly Counter<int> AlertaVencimentoCriado =
        Meter.CreateCounter<int>(
            "sgcf_alerta_vencimento_criado_total",
            description: "Número total de alertas de vencimento criados pelo job diário.");

    /// <summary>Incremented each time a bank exposure alert is created by AlertaExposicaoBancoJob.</summary>
    internal static readonly Counter<int> AlertaExposicaoCriado =
        Meter.CreateCounter<int>(
            "sgcf_alerta_exposicao_criado_total",
            description: "Número total de alertas de exposição de banco criados pelo job diário.");

    /// <summary>Incremented each time a monthly position snapshot is created by SnapshotMensalJob.</summary>
    internal static readonly Counter<int> SnapshotMensalCriado =
        Meter.CreateCounter<int>(
            "sgcf_snapshot_mensal_criado_total",
            description: "Número total de snapshots mensais de posição criados pelo job diário.");

    /// <summary>Incremented each time a daily interest accrual entry is created by ProvisaoJurosDiariaJob.</summary>
    internal static readonly Counter<int> ProvisaoJurosCriada =
        Meter.CreateCounter<int>(
            "sgcf_provisao_juros_criada_total",
            description: "Número total de lançamentos de provisão de juros criados pelo job diário.");
}

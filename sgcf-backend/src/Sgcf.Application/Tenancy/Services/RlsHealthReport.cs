using NodaTime;

namespace Sgcf.Application.Tenancy.Services;

/// <summary>
/// Resultado completo do healthcheck de Row-Level Security.
/// Status "healthy" indica que todos os checks passaram.
/// Status "unhealthy" indica que pelo menos um check falhou — ver <see cref="Checks"/> para diagnóstico.
/// </summary>
public sealed record RlsHealthReport(
    string Status,
    IReadOnlyList<RlsCheckResult> Checks,
    Instant VerificadoEm);

/// <summary>
/// Resultado de um check individual de RLS.
/// </summary>
/// <param name="Name">Identificador do check (snake_case).</param>
/// <param name="Status">"passed" ou "failed".</param>
/// <param name="Details">Diagnóstico legível. Em falha, lista tabelas ou descreve o problema.</param>
public sealed record RlsCheckResult(string Name, string Status, string Details);

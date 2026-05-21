using System.Data;
using Microsoft.Extensions.Logging;
using NodaTime;
using Npgsql;
using Sgcf.Application.Tenancy;
using Sgcf.Application.Tenancy.Services;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Infrastructure.Tenancy;

/// <summary>
/// Implementação de <see cref="IRlsHealthCheckService"/> que executa 4 checks de RLS via SQL direto.
///
/// Usa <see cref="IDbConnectionFactory"/> para conexões sem interceptors de tenant —
/// isso é intencional: os canary checks precisam observar o comportamento bruto do banco.
/// </summary>
internal sealed partial class RlsHealthCheckService(
    IDbConnectionFactory connFactory,
    ITenantRepository tenantRepo,
    IClock clock,
    ILogger<RlsHealthCheckService> logger) : IRlsHealthCheckService
{
    // Lista canônica das tabelas tenant-scoped — deve ser mantida em sincronia com
    // a migration EnableRowLevelSecurity (S13). Adicionada aqui como fonte de verdade
    // para os checks de saúde; a migration é a fonte de verdade para o schema.
    private static readonly string[] TabelasTenantScoped =
    [
        "contrato", "parcela", "garantia", "cronograma_pagamento",
        "finimp_detail", "lei4131_detail", "refinimp_detail",
        "nce_detail", "balcao_caixa_detail", "fgi_detail",
        "garantia_cdb_cativo_detail", "garantia_sblc_detail",
        "garantia_aval_detail", "garantia_alienacao_fiduciaria_detail",
        "garantia_duplicatas_detail", "garantia_recebiveis_cartao_detail",
        "garantia_boleto_bancario_detail", "garantia_fgi_detail",
        "cotacao", "proposta", "economia_negociacao",
        "limite_banco",
        "instrumento_hedge", "posicao_snapshot",
        "alerta_vencimento", "alerta_exposicao_banco",
        "simulacao_antecipacao", "simulacao_contratacao", "cenario_simulacao",
        "ebitda_mensal", "snapshot_mensal_posicao",
        "lancamento_contabil", "plano_contas_gerencial",
        "parametro_sistema", "parametro_cotacao",
        "audit_log",
    ];

    public async Task<RlsHealthReport> CheckAsync(CancellationToken ct)
    {
        List<RlsCheckResult> results = new(4);

        results.Add(await CheckRlsEnabledAsync(ct));
        results.Add(await CheckPoliciesPresentAsync(ct));
        results.Add(await CheckIsolationCanaryNoContextAsync(ct));
        results.Add(await CheckIsolationCanaryWithProxysAsync(ct));

        string status = results.All(r => r.Status == "passed") ? "healthy" : "unhealthy";
        Instant verificadoEm = clock.GetCurrentInstant();
        string verificadoEmStr = verificadoEm.ToString();

        LogHealthCheckResult(logger, status, verificadoEmStr);

        foreach (RlsCheckResult check in results.Where(r => r.Status == "failed"))
        {
            LogCheckFailed(logger, check.Name, check.Details);
        }

        return new RlsHealthReport(status, results.AsReadOnly(), verificadoEm);
    }

    /// <summary>Verifica que RLS (FORCE ROW LEVEL SECURITY) está ativa em todas as tabelas.</summary>
    private async Task<RlsCheckResult> CheckRlsEnabledAsync(CancellationToken ct)
    {
        const string CheckName = "rls_enabled_all_tables";

        try
        {
            using IDbConnection conn = await connFactory.CreateOpenConnectionAsync(ct);

            List<string> tabelasSemRls = await ExecuteQueryAsync(conn, @"
                SELECT c.relname
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'sgcf'
                  AND c.relkind = 'r'
                  AND c.relname = ANY(@tabelas)
                  AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity)",
                new NpgsqlParameter("tabelas", TabelasTenantScoped),
                ct);

            if (tabelasSemRls.Count == 0)
            {
                return new RlsCheckResult(CheckName, "passed",
                    $"{TabelasTenantScoped.Length} tabelas com RLS habilitada.");
            }

            return new RlsCheckResult(CheckName, "failed",
                $"Tabelas sem RLS: [{string.Join(", ", tabelasSemRls.Select(t => $"'{t}'"))}]");
        }
        catch (Exception ex)
        {
            LogCheckException(logger, CheckName, ex);
            return new RlsCheckResult(CheckName, "failed", $"Erro ao verificar RLS: {ex.Message}");
        }
    }

    /// <summary>Verifica que a policy tenant_isolation está presente em todas as tabelas.</summary>
    private async Task<RlsCheckResult> CheckPoliciesPresentAsync(CancellationToken ct)
    {
        const string CheckName = "policies_present";

        try
        {
            using IDbConnection conn = await connFactory.CreateOpenConnectionAsync(ct);

            List<string> tabelasComPolicy = await ExecuteQueryAsync(conn, @"
                SELECT tablename
                FROM pg_policies
                WHERE schemaname = 'sgcf'
                  AND tablename = ANY(@tabelas)
                  AND policyname = 'tenant_isolation'",
                new NpgsqlParameter("tabelas", TabelasTenantScoped),
                ct);

            List<string> tabelasSemPolicy = TabelasTenantScoped
                .Except(tabelasComPolicy, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tabelasSemPolicy.Count == 0)
            {
                return new RlsCheckResult(CheckName, "passed",
                    $"{TabelasTenantScoped.Length} policies tenant_isolation encontradas.");
            }

            return new RlsCheckResult(CheckName, "failed",
                $"Tabelas sem policy: [{string.Join(", ", tabelasSemPolicy.Select(t => $"'{t}'"))}]");
        }
        catch (Exception ex)
        {
            LogCheckException(logger, CheckName, ex);
            return new RlsCheckResult(CheckName, "failed", $"Erro ao verificar policies: {ex.Message}");
        }
    }

    /// <summary>
    /// Canary: sem app.tenant_id setado, uma query em sgcf.contrato deve retornar 0 linhas.
    /// Isso valida que o RLS isola corretamente conexões sem contexto de tenant.
    /// </summary>
    private async Task<RlsCheckResult> CheckIsolationCanaryNoContextAsync(CancellationToken ct)
    {
        const string CheckName = "isolation_canary_no_context";

        try
        {
            using IDbConnection conn = await connFactory.CreateOpenConnectionAsync(ct);
            // Conexão bruta sem SET app.tenant_id — RLS deve retornar 0 linhas.
            long count = await ExecuteCountAsync(conn, "SELECT COUNT(*) FROM sgcf.contrato", ct);

            return count == 0
                ? new RlsCheckResult(CheckName, "passed",
                    "Conexão sem app.tenant_id retornou 0 linhas em contrato.")
                : new RlsCheckResult(CheckName, "failed",
                    $"RLS não está funcionando: retornou {count} linhas sem app.tenant_id setado.");
        }
        catch (Exception ex)
        {
            LogCheckException(logger, CheckName, ex);
            return new RlsCheckResult(CheckName, "failed", $"Erro no canary sem contexto: {ex.Message}");
        }
    }

    /// <summary>
    /// Canary: com app.tenant_id do tenant Proxys, a query em sgcf.contrato deve executar sem erro.
    /// Valida que o RLS permite acesso correto quando o contexto está configurado.
    /// </summary>
    private async Task<RlsCheckResult> CheckIsolationCanaryWithProxysAsync(CancellationToken ct)
    {
        const string CheckName = "isolation_canary_with_proxys";

        try
        {
            Tenant? proxys = await tenantRepo.GetBySlugAsync("proxys", ct);

            if (proxys is null)
            {
                return new RlsCheckResult(CheckName, "passed",
                    "Tenant proxys não encontrado — check ignorado (ambiente sem seed).");
            }

            using IDbConnection conn = await connFactory.CreateOpenConnectionAsync(ct);

            // Set the tenant context for this raw connection so RLS filters by proxys tenant.
            await ExecuteNonQueryAsync(conn,
                $"SET LOCAL app.tenant_id = '{proxys.Id}'", ct);

            long count = await ExecuteCountAsync(conn, "SELECT COUNT(*) FROM sgcf.contrato", ct);

            return new RlsCheckResult(CheckName, "passed",
                $"Conexão com tenant proxys retornou {count} linhas em contrato.");
        }
        catch (Exception ex)
        {
            LogCheckException(logger, CheckName, ex);
            return new RlsCheckResult(CheckName, "failed", $"Erro no canary com proxys: {ex.Message}");
        }
    }

    // ── SQL helpers ──────────────────────────────────────────────────────────

    private static async Task<List<string>> ExecuteQueryAsync(
        IDbConnection conn,
        string sql,
        NpgsqlParameter parameter,
        CancellationToken ct)
    {
        await using NpgsqlCommand cmd = new(sql, (NpgsqlConnection)conn);
        cmd.Parameters.Add(parameter);

        List<string> results = new();
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private static async Task<long> ExecuteCountAsync(
        IDbConnection conn,
        string sql,
        CancellationToken ct)
    {
        await using NpgsqlCommand cmd = new(sql, (NpgsqlConnection)conn);
        object? result = await cmd.ExecuteScalarAsync(ct);
        return result is long l ? l : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteNonQueryAsync(
        IDbConnection conn,
        string sql,
        CancellationToken ct)
    {
        await using NpgsqlCommand cmd = new(sql, (NpgsqlConnection)conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Structured logging ───────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "RLS healthcheck concluído — status: {Status}, verificadoEm: {VerificadoEm}")]
    private static partial void LogHealthCheckResult(ILogger logger, string status, string verificadoEm);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RLS check '{CheckName}' falhou — detalhes: {Details}")]
    private static partial void LogCheckFailed(ILogger logger, string checkName, string details);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RLS check '{CheckName}' lançou exceção")]
    private static partial void LogCheckException(ILogger logger, string checkName, Exception ex);
}

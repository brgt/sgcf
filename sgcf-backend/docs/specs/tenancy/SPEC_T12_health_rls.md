# SPEC — Task −1.12 — Healthcheck `/health/rls` + Métricas de Tenancy

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_multi_tenancy.md` Task −1.12
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Dependências:** Task −1.7

---

## 1. Objetivo

Endpoint de saúde que valida em runtime que o RLS está habilitado e funcionando em todas as tabelas tenant-scoped. Adiciona métricas de observabilidade de tenancy. Cria runbook operacional.

Detecta falha de policy ausente, RLS desabilitada acidentalmente, ou cenário onde a aplicação consegue ler dados sem `app.tenant_id` setado.

---

## 2. Endpoint

```
GET /health/rls
```

**Auth:** `Policies.SuperAdmin`.

### 2.1 Response 200 (healthy)

```json
{
  "status": "healthy",
  "checks": [
    {
      "name": "rls_enabled_all_tables",
      "status": "passed",
      "details": "30 tabelas com RLS habilitada."
    },
    {
      "name": "policies_present",
      "status": "passed",
      "details": "30 policies tenant_isolation encontradas."
    },
    {
      "name": "isolation_canary_no_context",
      "status": "passed",
      "details": "Conexão sem app.tenant_id retornou 0 linhas em contrato."
    },
    {
      "name": "isolation_canary_with_proxys",
      "status": "passed",
      "details": "Conexão com tenant proxys retornou 142 linhas em contrato."
    }
  ],
  "verificadoEm": "2026-05-20T14:32:00Z"
}
```

### 2.2 Response 503 (unhealthy)

```json
{
  "status": "unhealthy",
  "checks": [
    {
      "name": "rls_enabled_all_tables",
      "status": "failed",
      "details": "Tabelas sem RLS: ['alerta_vencimento']"
    },
    /* ... */
  ],
  "verificadoEm": "2026-05-20T14:32:00Z"
}
```

Status code 503 sinaliza monitoring externo (Cloud Run health probe, Prometheus blackbox, etc.) que algo está errado.

---

## 3. Checks implementados

### 3.1 `rls_enabled_all_tables`

```sql
SELECT c.relname
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'sgcf'
  AND c.relkind = 'r'
  AND c.relname = ANY(@tenant_scoped_tables)
  AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity);
```

`@tenant_scoped_tables` é injetado pela aplicação a partir da lista canônica.

**Pass:** zero linhas retornadas.

### 3.2 `policies_present`

```sql
SELECT tablename
FROM pg_policies
WHERE schemaname = 'sgcf'
  AND tablename = ANY(@tenant_scoped_tables)
  AND policyname = 'tenant_isolation';
```

**Pass:** quantidade igual à lista de tabelas esperadas.

### 3.3 `isolation_canary_no_context`

Conecta usando `sgcf_app` (role sem bypass), **sem** setar `app.tenant_id`, e executa:

```sql
SELECT COUNT(*) FROM sgcf.contrato;
```

**Pass:** retorna 0.

### 3.4 `isolation_canary_with_proxys`

Conecta usando `sgcf_app`, seta `app.tenant_id = '<proxys-uuid>'`, e executa:

```sql
SELECT COUNT(*) FROM sgcf.contrato;
```

**Pass:** retorna > 0 (ou 0 se tenant Proxys ainda não tem contratos, mas a query não lança erro).

---

## 4. Serviço `RlsHealthCheckService`

```csharp
namespace Sgcf.Application.Tenancy.Services;

public interface IRlsHealthCheckService
{
    Task<RlsHealthReport> CheckAsync(CancellationToken ct);
}

public sealed record RlsHealthReport(
    string Status, // "healthy" | "unhealthy"
    IReadOnlyList<RlsCheckResult> Checks,
    Instant VerificadoEm);

public sealed record RlsCheckResult(string Name, string Status, string Details);

internal sealed class RlsHealthCheckService(
    IDbConnectionFactory connFactory,  // factory para criar conexão como sgcf_app
    ITenantRepository tenantRepo,
    IClock clock,
    ILogger<RlsHealthCheckService> logger) : IRlsHealthCheckService
{
    public async Task<RlsHealthReport> CheckAsync(CancellationToken ct)
    {
        List<RlsCheckResult> results = new();

        results.Add(await CheckRlsEnabledAsync(ct));
        results.Add(await CheckPoliciesPresentAsync(ct));
        results.Add(await CheckIsolationCanaryAsync(ct));
        results.Add(await CheckProxysCanaryAsync(ct));

        string status = results.All(r => r.Status == "passed") ? "healthy" : "unhealthy";

        return new RlsHealthReport(status, results, clock.GetCurrentInstant());
    }

    // helpers privados...
}
```

---

## 5. Controller

```csharp
namespace Sgcf.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IRlsHealthCheckService rlsCheck) : ControllerBase
{
    [HttpGet("rls")]
    [Authorize(Policy = Policies.SuperAdmin)]
    public async Task<IActionResult> Rls(CancellationToken ct)
    {
        RlsHealthReport report = await rlsCheck.CheckAsync(ct);
        int status = report.Status == "healthy" ? 200 : 503;
        return StatusCode(status, report);
    }
}
```

---

## 6. Métricas Prometheus (opcional, conforme infra)

Se infraestrutura já suporta Prometheus:

```csharp
internal static class TenancyMetrics
{
    public static readonly Counter RequestsTotal =
        Metrics.CreateCounter(
            "sgcf_tenant_requests_total",
            "Requisições HTTP por tenant",
            new CounterConfiguration { LabelNames = new[] { "tenant_slug", "endpoint", "status_code" } });

    public static readonly Gauge ActiveTenants =
        Metrics.CreateGauge(
            "sgcf_tenant_active_total",
            "Tenants ativos no sistema");

    public static readonly Counter RlsCheckFailures =
        Metrics.CreateCounter(
            "sgcf_rls_check_failures_total",
            "Falhas em healthcheck de RLS",
            new CounterConfiguration { LabelNames = new[] { "check_name" } });

    public static readonly Counter ImpersonationOperations =
        Metrics.CreateCounter(
            "sgcf_tenant_impersonation_total",
            "Operações com impersonação cross-tenant",
            new CounterConfiguration { LabelNames = new[] { "tenant_slug", "actor_sub", "operation" } });
}
```

Caso infra ainda não tenha Prometheus, logar eventos estruturados equivalentes com `ILogger` (consumível por GCP Cloud Logging).

---

## 7. Runbook operacional

`docs/operacao/multi-tenancy.md` (criar nesta task):

### Seções obrigatórias

1. **Visão geral** — arquitetura shared schema + RLS.
2. **Onboarding de novo tenant** — passo a passo (criar via admin, provisionar, validar `/health/rls`).
3. **Suspensão** — quando suspender, como reativar, impacto em sessões ativas.
4. **Impersonação assistida** — como super-admin acessa dados do cliente.
5. **Troubleshooting**
   - `/health/rls` retornando 503 — diagnóstico.
   - Cliente reporta que não vê dados próprios — checar status, provisionamento, claims do JWT.
   - Suspeita de vazamento cross-tenant — query forense.
6. **Métricas e alertas** — quais dashboards monitorar.
7. **Procedimento de incident response** — script de bloqueio rápido (suspender tenant) e investigação.

---

## 8. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Tabela tenant-scoped criada sem RLS (bug humano) | `/health/rls` retorna 503 listando a tabela |
| Policy deletada por DBA | Check `policies_present` falha |
| Tenant Proxys ainda sem dados | Canary `with_proxys` retorna 0 mas não falha (executou sem erro) |
| Roles `sgcf_app` ou `sgcf_super` faltando | Check lança exceção tratada; resposta 503 com `details` |
| Endpoint chamado sem auth | 401 |
| Endpoint chamado por `admin` (não super-admin) | 403 |
| Falha de conexão Postgres | 503 com `details: "Database unreachable"` |

---

## 9. Critérios de Aceite

- [ ] `IRlsHealthCheckService` + impl em `Sgcf.Application.Tenancy.Services`.
- [ ] Endpoint `GET /health/rls` operacional + autorização `Policies.SuperAdmin`.
- [ ] 4 checks listados na §3 implementados.
- [ ] Resposta inclui detalhamento por check em falha.
- [ ] Métricas Prometheus expostas (ou logs estruturados equivalentes).
- [ ] `docs/operacao/multi-tenancy.md` publicado com seções §7.
- [ ] Teste: derrubar policy de uma tabela → 503 + tabela listada.
- [ ] Teste: endpoint sem super-admin → 403.

---

## 10. Verificação

```bash
dotnet test --filter "FullyQualifiedName~RlsHealthCheck"

# Smoke
curl -H "Authorization: Bearer $SUPER_ADMIN_TOKEN" http://localhost:5000/health/rls | jq

# Forçar falha (em ambiente de teste)
psql -U sgcf_migrator -d sgcf_dev -c "DROP POLICY tenant_isolation ON sgcf.contrato;"
curl -H "Authorization: Bearer $SUPER_ADMIN_TOKEN" http://localhost:5000/health/rls | jq '.status'
# → "unhealthy"
```

**Teste-chave:**

```csharp
[Fact]
[Trait("Category", "Slow")]
public async Task HealthRls_quando_policy_falta_retorna_503()
{
    await _pg.ExecuteAsync("DROP POLICY IF EXISTS tenant_isolation ON sgcf.contrato;");

    using var client = _fx.SuperAdminClient();
    var response = await client.GetAsync("/health/rls");

    response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    var report = await response.Content.ReadFromJsonAsync<RlsHealthReport>();
    report!.Checks.Should().Contain(c => c.Name == "policies_present" && c.Status == "failed");
}

[Fact]
public async Task HealthRls_sem_super_admin_retorna_403()
{
    using var client = _fx.ClientFor(_tenantProxys); // role admin
    var response = await client.GetAsync("/health/rls");

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

---

## 11. Boundaries específicas

### 11.1 Always do
- Reportar diagnóstico detalhado em `details` quando check falha.
- Logar resultado do healthcheck em GCP Cloud Logging.
- Manter lista canônica de tabelas tenant-scoped centralizada (não duplicar em vários lugares).

### 11.2 Ask first
- Adicionar check novo que abre conexão extra (impacta latência do endpoint).
- Expor endpoint sem auth para Cloud Run probe (precisa avaliar exposição).

### 11.3 Never do
- Expor lista de tenants ativos no payload (potencial reconnaissance).
- Cachear resultado do healthcheck por mais de 30 s (perde valor diagnóstico).
- Implementar a lógica como background job (precisa ser sob demanda).

---

## 12. Arquivos esperados

- `src/Sgcf.Application/Tenancy/Services/IRlsHealthCheckService.cs`
- `src/Sgcf.Application/Tenancy/Services/RlsHealthReport.cs`
- `src/Sgcf.Infrastructure/Tenancy/RlsHealthCheckService.cs`
- `src/Sgcf.Infrastructure/Tenancy/IDbConnectionFactory.cs`
- `src/Sgcf.Api/Controllers/HealthController.cs`
- `src/Sgcf.Api/Telemetry/TenancyMetrics.cs` (se Prometheus)
- `docs/operacao/multi-tenancy.md`
- `tests/Sgcf.Api.IntegrationTests/Tenancy/HealthControllerRlsTests.cs`
- `tests/Sgcf.Application.Tests/Tenancy/RlsHealthCheckServiceTests.cs`

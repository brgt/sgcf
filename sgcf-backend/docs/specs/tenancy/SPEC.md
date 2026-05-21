# SPEC — Multi-Tenancy (Shared Schema + RLS)

> **Status:** Implementado (Tasks −1.1 a −1.12 entregues — Fase −1 completa)
> **Data:** 2026-05-20
> **Versão:** v1.1
> **Audiência:** Time de backend SGCF, líder de arquitetura, SRE, PO
> **Plano de execução:** `tasks/plan_multi_tenancy.md` v1.1
> **Decisões sponsor:** 2026-05-20 (registradas em §10)
> **Bloqueia:** `tasks/plan_cockpit_backend_gaps.md` Fase 0

---

## 1. Objetivo

Transformar o SGCF de instância única (`Proxys`) em SaaS multi-tenant, mantendo:

- **Um único cluster PostgreSQL** (custo operacional baixo).
- **Coluna `tenant_id NOT NULL`** em toda entidade operacional (~30 tabelas).
- **Postgres Row Level Security (RLS)** como segunda camada de isolação.
- **EF Core global query filter** como primeira camada (na aplicação).
- **Identificação padrão de mercado:** `Guid` PK + `Slug` kebab-case humano.

### 1.1 Personas

| Persona | Pergunta central | Cobertura |
|---------|-------------------|-----------|
| Operador interno Nordware (`super-admin`) | "Como provisionar, suspender e dar suporte a clientes?" | Endpoints `/admin/tenants/*`, impersonação assistida, healthcheck `/health/rls` |
| Administrador do cliente (`admin`) | "Como gerenciar parâmetros, usuários e configurações da minha empresa?" | Endpoints existentes com `Policies.Admin`, agora tenant-scoped |
| Usuários finais (`gerente`, `tesouraria`, `diretor`, etc.) | "Como operar contratos, cotações, hedges?" | Endpoints existentes, isolados ao seu tenant pelo JWT |

### 1.2 Métricas de sucesso

- **Zero vazamento cross-tenant** validado pela suite de testes de isolação (Task −1.8).
- **Provisionar novo tenant** completo em menos de 5 s (Task −1.6).
- **Overhead de RLS** menor que 5% no P95 dos endpoints de painel (benchmark antes/depois).
- **`/health/rls`** retorna 200 com todos os checks verdes.

---

## 2. Escopo

### 2.1 Dentro do escopo (Fase −1, antes do cockpit)

| Task | Descrição | SPEC dedicado |
|------|-----------|---------------|
| −1.1 | ADR-020 + `ITenantContext` | `SPEC_T01_adr_tenant_context.md` |
| −1.2 | Agregado `Tenant` + endpoints admin | `SPEC_T02_agregado_tenant.md` |
| −1.3 | JWT com `tenant_id` + `super-admin` + middleware | `SPEC_T03_jwt_middleware.md` |
| −1.4 | Marker `ITenantScoped` + EF global filter | `SPEC_T04_efcore_filter.md` |
| −1.5 | Migration big-bang | `SPEC_T05_migration_bigbang.md` |
| −1.6 | Provisionamento idempotente | `SPEC_T06_provisionamento.md` |
| −1.7 | RLS policies | `SPEC_T07_rls_policies.md` |
| −1.8 | Suite cross-tenant isolation | `SPEC_T08_cross_tenant_tests.md` |
| −1.9 | `ParametroSistema` per-tenant | `SPEC_T09_parametro_sistema.md` |
| −1.10 | `PlanoContasGerencial` per-tenant | `SPEC_T10_plano_contas.md` |
| −1.11 | `AuditLog` per-tenant | `SPEC_T11_audit_log.md` |
| −1.12 | Healthcheck `/health/rls` + métricas | `SPEC_T12_health_rls.md` |

### 2.2 Fora do escopo

Fase futura (revisitar com primeiro cliente externo contratado):

- DB por tenant (clientes enterprise).
- Schema por tenant.
- Data residency cross-region.
- SSO/SAML federado por tenant.
- Subdomínios por tenant.
- Faturamento por uso.
- Sandbox por tenant.
- Backup filtrado por tenant.

---

## 3. Tech Stack

Conforme `sgcf-backend/CLAUDE.md`:

- .NET 11 + ASP.NET Core 11 + EF Core 11.
- MediatR para Commands/Queries.
- NodaTime (`Instant`, `LocalDate`, `IClock`).
- PostgreSQL 16 (RLS habilitada por tabela).
- Redis 7 (cache de tenants ativos).
- xUnit + FsCheck + FluentAssertions + Testcontainers.

Pacotes novos previstos (Task −1.1):

- `UUIDNext` (v4) para geração de UUID v7 com ordenação temporal — fallback v4 quando indisponível.
- Nenhum novo provider de identidade (mantém JWT atual + claim adicional).

---

## 4. Commands

```bash
# Build / Test
dotnet build sgcf-backend.sln
dotnet test --filter "Category!=Slow"
dotnet test --filter "Category=CrossTenantIsolation"
dotnet test --collect:"XPlat Code Coverage"

# Migrations
dotnet ef migrations add <Nome> --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api

# Tenant admin (após Task −1.2)
curl -X POST http://localhost:5000/api/v1/admin/tenants \
  -H "Authorization: Bearer $SUPER_ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"slug":"acme-finance","nome":"ACME Finance Ltda","cnpj":"00.000.000/0001-00","plano":"Padrao"}'

# Healthcheck RLS (após Task −1.12)
curl -H "Authorization: Bearer $SUPER_ADMIN_TOKEN" \
  http://localhost:5000/health/rls
```

---

## 5. Project Structure

Módulos novos introduzidos por esta entrega:

```
src/Sgcf.Domain/Tenancy/                ← Agregado Tenant + enums + ITenantScoped marker
src/Sgcf.Application/Tenancy/           ← ITenantContext, Commands, Queries, ITenantRepository
src/Sgcf.Infrastructure/Tenancy/        ← TenantContext concreto, TenantCache, interceptors
src/Sgcf.Api/Middleware/TenantResolverMiddleware.cs
src/Sgcf.Api/Controllers/TenantsController.cs
src/Sgcf.Api/Controllers/HealthController.cs (ou Health/RlsHealthController)
docs/adr/ADR-020-multi-tenancy-shared-schema-rls.md
docs/operacao/multi-tenancy.md          ← Runbook operacional para o time interno
docs/specs/tenancy/                     ← Estes SPECs
```

Módulos existentes modificados:

```
src/Sgcf.Domain/<todas-as-pastas>/      ← Agregados ganham TenantId + ITenantScoped
src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs (filter + interceptors)
src/Sgcf.Infrastructure/Persistence/Configurations/*.cs (HasIndex tenant_id)
src/Sgcf.Application/Authorization/Policies.cs (SuperAdmin)
src/Sgcf.Api/Program.cs (middleware + DI)
src/Sgcf.Api/Filters/IdempotencyFilter.cs (chave prefixada com tenant_id)
```

---

## 6. Code Style

Snippet representativo (handler com contexto de tenant injetado):

```csharp
using MediatR;
using Sgcf.Application.Tenancy;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Queries;

public sealed record ListContratosQuery(ContratoFilter Filter)
    : IRequest<PagedResult<ContratoDto>>;

public sealed class ListContratosQueryHandler(
    IContratoRepository contratoRepo,
    ITenantContext tenantContext) // injetado em todo handler que toca tenant-scoped
    : IRequestHandler<ListContratosQuery, PagedResult<ContratoDto>>
{
    public async Task<PagedResult<ContratoDto>> Handle(
        ListContratosQuery query,
        CancellationToken ct)
    {
        // EF Core global filter: IsResolved && TenantId == atual.
        // Quando IsResolved=false (jobs, migrations), o filter retorna 0 linhas.
        // Handler NÃO adiciona WHERE tenant_id manualmente — confia no filter.
        IReadOnlyList<Contrato> contratos = await contratoRepo.ListAsync(query.Filter, ct);

        // Operação adicional sensível a tenant: log com contexto.
        // Não usar tenantContext.TenantId para WHERE manual — confiar no filter.

        return new PagedResult<ContratoDto>(/* ... */);
    }
}
```

> **Nota de implementação (v1.1):** O filtro foi corrigido de `!IsResolved || TenantId == atual`
> para `IsResolved && TenantId == atual`. A lógica antiga expunha todos os dados quando o contexto
> não estava resolvido. A lógica atual retorna zero linhas em contextos não resolvidos.
>
> Exceções de domínio tipadas adicionadas: `TenantSuspendoException` e `TenantArquivadoException`
> substituem `InvalidOperationException` com message-string matching em `TenantProvisioner` e
> `TenantsController`.

**Convenções aplicáveis:**

- Entidade operacional **sempre** implementa `ITenantScoped` desde o nascimento.
- Handler que precisa de `TenantId` para auditoria ou logs **injeta `ITenantContext`** explicitamente.
- Handler **nunca** adiciona `WHERE tenant_id = ...` manualmente — depende do filter.
- Operações que precisam ignorar isolação (super-admin cross-tenant) usam helper explícito `WithTenantBypassAsync` + auditoria obrigatória.
- Conexão DB **sempre** seta `app.tenant_id` no início de cada transação (via interceptor).
- Identificação de tenant em URL admin aceita `{idOrSlug}` (UUID ou slug humano).

---

## 7. Testing Strategy

| Nível | Frame | Objetivo | Onde |
|-------|-------|----------|------|
| Unit | xUnit + FsCheck | Lógica do agregado `Tenant`, validação de slug, máquina de estados | `tests/Sgcf.Domain.Tests/Tenancy/` |
| Integration | Testcontainers Postgres | EF filter + RLS + repositories | `tests/Sgcf.Application.Tests/Tenancy/` |
| API | WebApplicationFactory | Endpoints admin + middleware + autorização | `tests/Sgcf.Api.IntegrationTests/Tenancy/` |
| **Cross-tenant isolation** | WebApplicationFactory + 2 tenants | **Garantia de zero vazamento** | `tests/Sgcf.Api.IntegrationTests/CrossTenantIsolation/` |
| Migration | Testcontainers + base sintética | Big-bang migration aplica/reverte | `tests/Sgcf.Application.Tests/Migrations/` |

**Cobertura obrigatória:**

- 100% das máquinas de estado em `Tenant`.
- 100% dos handlers admin.
- 100% dos controllers existentes recebem caso de teste cross-tenant (suite Task −1.8).
- Healthcheck `/health/rls` cobre cenários positivo + degradado.

**Tags xUnit:**

- `[Trait("Category", "Slow")]` — testes com Testcontainers.
- `[Trait("Category", "CrossTenantIsolation")]` — suite específica que bloqueia merge se vermelha.

---

## 8. Boundaries

### 8.1 Always do

- Injetar `ITenantContext` em handler que toca entidade tenant-scoped (para auditoria/logs).
- Implementar `ITenantScoped` em toda entidade nova de domínio operacional.
- Aplicar `Policies.SuperAdmin` em endpoints administrativos.
- Setar `app.tenant_id` no Postgres em cada transação (via interceptor).
- Prefixar `Idempotency-Key` com `tenant_id` para evitar colisão cross-tenant.
- Registrar em `AuditLog` toda mutação feita por `super-admin` em nome de outro tenant.
- Documentar nova entidade no runbook `docs/operacao/multi-tenancy.md`.
- Hosted services em `Sgcf.Jobs` iteram explicitamente sobre tenants ativos.

### 8.2 Ask first

- Adicionar tabela tenant-scoped sem RLS policy (proibido por padrão, exige aprovação).
- Bypassar filter EF Core (`IgnoreQueryFilters`) — exige justificativa em PR.
- Aceitar `X-Tenant-Id` em endpoint não-admin.
- Mudar política de cache de tenants (invalidação, TTL).
- Mover tabela de "tenant-scoped" para "global" — exige análise de impacto LGPD.
- Tabela com FK composite que não inclui `tenant_id` em ambas as pontas.

### 8.3 Never do

- Adicionar `WHERE tenant_id = ...` manualmente em handler (confiar no filter).
- Persistir entidade `ITenantScoped` sem `ITenantContext` no scope (já lança exceção, não silenciar).
- Aceitar `tenant_id` via query param ou body em endpoint operacional.
- Permitir `super-admin` acessar `Policies.Admin` sem impersonação explícita (privilege escalation).
- Compartilhar `Idempotency-Key` entre tenants.
- Logar dados de outro tenant a partir do contexto de um tenant (vazamento via logs).
- Disable RLS em tabela tenant-scoped em produção (DDL bloqueado fora de migration).
- Confiar apenas em RLS (manter EF filter como primeira camada) ou apenas em EF filter (manter RLS como segunda camada).
- Endpoints `/admin/*` sem `Policies.SuperAdmin`.

---

## 9. Success Criteria

A entrega é considerada concluída quando:

- [ ] 12 SPECs detalhados implementados, com testes passando em CI.
- [ ] Suite `CrossTenantIsolation` verde para todos os controllers.
- [ ] Tenant `proxys` (atual) opera normalmente sem regressão funcional.
- [ ] É possível criar tenant `cliente-acme` via `/admin/tenants`, provisionar, e operar em paralelo.
- [ ] `GET /health/rls` retorna 200 com todos os checks verdes.
- [ ] Overhead RLS < 5% no P95 dos painéis (benchmark documentado).
- [ ] ADR-020 publicado.
- [ ] `docs/operacao/multi-tenancy.md` publicado e revisado pelo SRE.
- [ ] Fase 0 do `plan_cockpit_backend_gaps.md` desbloqueada.

---

## 10. Decisões do Sponsor — 2026-05-20

1. **Identificação:** `Guid` PK + `Slug` kebab-case (padrão de mercado: Stripe, AWS, GitHub).
2. **Backup/restore:** fora de escopo agora. Revisitar com primeiro cliente externo.
3. **Clientes externos hoje:** nenhum (sistema não lançado). Sem janela de coexistência.
4. **Roles:** `super-admin` exclusivo Nordware; clientes finais ficam com `admin` ou abaixo. `Policies.SuperAdmin` adicionada; `Policies.Admin` continua valendo dentro do tenant.
5. **Healthcheck `/health/rls`:** confirmado (Task −1.12).

## 11. Refinamentos remanescentes (não bloqueantes)

- Política de invalidação de cache de tenants em cluster multi-instância (Redis pub/sub vs polling 30 s) — antes da Task −1.3.
- Visibilidade de impersonação `super-admin → tenant` ao admin do tenant impersonado — antes da Task −1.11.

---

## 12. Referências

- `tasks/plan_multi_tenancy.md` v1.1
- `sgcf-backend/CLAUDE.md`
- `docs/adr/ADR-020-multi-tenancy-shared-schema-rls.md` (a criar em Task −1.1)
- PostgreSQL RLS: https://www.postgresql.org/docs/16/ddl-rowsecurity.html
- EF Core global query filters: https://learn.microsoft.com/en-us/ef/core/querying/filters
- Stripe API design (tenant + slug): https://stripe.com/docs/api

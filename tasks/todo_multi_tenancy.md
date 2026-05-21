# Todo — Multi-Tenancy (Shared Schema + RLS)

> Plano detalhado: `tasks/plan_multi_tenancy.md` v1.2
> Antecede a Fase 0 do plano `tasks/plan_cockpit_backend_gaps.md`.
> Marque cada item ao concluir.

## Fase A — Fundação (Sprints 1 a 2)

- [x] **−1.1** ADR-020 + interface `ITenantContext` + `TenantContext` DI scoped.
- [x] **−1.2** Agregado `Tenant` (Guid PK + slug humano) + repositório + migration + `TenantsController` admin.
- [x] **−1.3** JWT com claim `tenant_id` + role `super-admin` (Nordware) + `Policies.SuperAdmin` + `TenantResolverMiddleware` + `TenantCache` (Redis pub/sub + TTL 5min).
- [x] **−1.4** Marker `ITenantScoped` + global query filter EF Core + `SaveChanges` interceptor.
- [x] **−1.5** Migration big-bang: `tenant_id` em ~30 tabelas + backfill `tenant proxys` + unique constraints reescritos.

### Checkpoint A — Fundação Pronta
- [x] `dotnet test` verde.
- [x] Filter cross-tenant funcional em teste.
- [x] Tenant `proxys` carregado.
- [x] Endpoints existentes operacionais com claim `tenant_id`.

---

## Fase B — Provisionamento e Operação (Sprints 3 a 4)

- [x] **−1.6** Provisionamento idempotente (`POST /admin/tenants/{id}/provisionar`).
- [x] **−1.7** RLS policies + `DbConnectionInterceptor` setando `app.tenant_id`.
- [x] **−1.8** Suite cross-tenant isolation (14+ controllers) + `ci-cross-tenant.yml` + `CODEOWNERS` + branch protection.

### Checkpoint B — Isolação Validada
- [x] Suite cross-tenant 100% verde em CI.
- [x] RLS habilitada.
- [x] Pen-test interno passa (sem vazamento).

---

## Fase C — Configuração Per-Tenant (Sprint 5)

- [x] **−1.9** `ParametroSistema` per-tenant + `ValidadorTetaoMensal` por tenant.
- [x] **−1.10** `PlanoContasGerencial` per-tenant + clonagem na provisão.
- [x] **−1.11** `AuditLog` per-tenant + endpoint admin cross-tenant + transparência obrigatória de impersonação.
- [x] **−1.12** Healthcheck `/health/rls` + métricas de tenancy + `docs/operacao/multi-tenancy.md`.

### Checkpoint C — Pronto para o Cockpit
- [x] Demonstração de operar 2 tenants em paralelo.
- [x] Tenant `proxys` sem regressão.
- [x] **Fase 0 do cockpit liberada.**

---

## Fase D — Adaptações no Cockpit (junto com plan_cockpit_backend_gaps.md)

Inclusos no SPEC de cada task original. Não são novas tasks.

- [ ] `Alerta` (Task 0.2 cockpit) implementa `ITenantScoped` desde o nascimento.
- [ ] `Alerta.ChaveIdempotencia` inclui prefixo `tenant_id`.
- [ ] Rules engine (Task 0.4 cockpit) itera sobre tenants ativos.
- [ ] `ContaBancaria`, `SaldoCaixa`, `EventoFluxoCaixa`, `DadosContabeisMensal` nascem tenant-scoped.
- [ ] Atualizar SPECs do cockpit em `sgcf-backend/docs/specs/cockpit/` com nota de tenancy.

---

## Decisões do Sponsor — 2026-05-20

- [x] Identificação de tenant: Guid PK + slug kebab-case (padrão de mercado).
- [x] Backup/restore: fora de escopo agora; revisitar com primeiro cliente externo.
- [x] MCP/A2A: sistema não lançado, sem clientes externos — tenant obrigatório desde o dia 1.
- [x] Roles: `super-admin` exclusivo Nordware; demais usuários ficam em `admin` (escopo tenant) ou abaixo.
- [x] Healthcheck `/health/rls` confirmado (Task −1.12).
- [x] Invalidação de cache de tenants: Redis pub/sub + TTL local 5min (Task −1.3).
- [x] Visibilidade de impersonação: transparência total e obrigatória (Task −1.11).
- [x] Branch protection: ativada em `main` com gate `Cross-Tenant Isolation Tests` + CODEOWNERS (Task −1.8). Workflow e runbook entregues; configuração no GitHub feita pelo administrador.

## Refinamentos remanescentes

Nenhum. Todas as decisões foram tomadas.

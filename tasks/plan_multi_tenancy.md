# Plano de Implementação — Multi-Tenancy (Shared Schema + RLS)

**Versão:** 1.2 — incorpora decisões adicionais do sponsor de 2026-05-20 (cache, transparência, branch protection).
**Data:** 2026-05-20
**Estratégia confirmada com sponsor:** shared schema + coluna `tenant_id` + Postgres Row Level Security.
**Esforço estimado:** 4 a 6 sprints (Fase −1, antes da Fase 0 do cockpit).
**Documento âncora desta entrega:** este plano.
**Dependentes:** `tasks/plan_cockpit_backend_gaps.md` v1.1 (Fase 0 só inicia após Checkpoint C deste plano).

---

## 1. Overview

Transformar o SGCF de monoinstância (`tenant_default = Proxys`) em multi-tenant, mantendo um único banco PostgreSQL com isolação por coluna `tenant_id NOT NULL` em toda entidade operacional, reforçada por Row Level Security do Postgres como rede de segurança em caso de falha no filtro EF Core.

**Resultado esperado:** novo cliente entra em produção via endpoint admin `POST /api/v1/tenants` + provisionamento idempotente, sem janela de manutenção.

---

## 2. Decisões Arquiteturais

### 2.1 Isolação em duas camadas

- **Camada 1 (aplicação):** EF Core global query filter aplica `WHERE tenant_id = current_setting('app.tenant_id')` em toda entidade tenant-scoped.
- **Camada 2 (banco):** RLS policies em cada tabela tenant-scoped enforce a mesma regra no nível do PostgreSQL. Qualquer query (inclusive psql do DBA) precisa setar `app.tenant_id` para enxergar dados.

Duas camadas porque: se um handler novo esquecer o filtro, o RLS impede vazamento.

### 2.2 Coluna `tenant_id` em entidades filhas

Tabelas como `parcela`, `garantia`, `finimp_detail` recebem `tenant_id` próprio (denormalizado) — não dependem de JOIN com `contrato` para filtrar. Razão: evita N+1 em queries de painel e simplifica RLS policy (uma policy por tabela).

Trigger SQL valida consistência: `parcela.tenant_id = contrato.tenant_id` (CHECK constraint via foreign key composite `(tenant_id, contrato_id)`).

### 2.3 Classificação de entidades

#### Tenant-scoped (recebem `tenant_id NOT NULL`)

**Domínio operacional** (instância específica do cliente):

| Categoria | Tabelas |
|-----------|---------|
| Contratos | `contrato`, `parcela`, `garantia`, `evento_cronograma`, `finimp_detail`, `lei4131_detail`, `refinimp_detail`, `nce_detail`, `capital_de_giro_detail`, `fgi_detail`, todas `garantia_*_detail` |
| Cotações | `cotacao`, `proposta`, `economia_negociacao`, `limite_banco`, `limite_banco_historico`, `garantia_exigida_limite` |
| Hedge | `instrumento_hedge`, `posicao_snapshot` |
| Alertas | `alerta_vencimento`, `alerta_exposicao_banco`, `alertas` (novo da Task 0.2 do cockpit) |
| Simulações | `simulacao_antecipacao`, `simulacao_contratacao`, `cenario_simulacao` |
| Painel | `ebitda_mensal` (→ `dados_contabeis_mensal`), `snapshot_mensal_posicao` |
| Contabilidade | `lancamento_contabil`, `plano_contas_gerencial` |
| Tesouraria (Fase 3 cockpit) | `conta_bancaria`, `saldo_caixa`, `evento_fluxo_caixa` |
| Configuração | `parametro_sistema` (tetão mensal vira per-tenant), `parametro_cotacao` |
| Auditoria | `audit_log` |

#### Globais (sem `tenant_id`)

**Catálogos e séries de referência** (universais):

| Tabela | Razão |
|--------|-------|
| `banco_config` | Cadastro de instituições Compe — Itaú, Santander são iguais para todos os clientes |
| `cotacao_fx` | PTAX e câmbio são referência universal |
| `cdi_snapshot` | CDI é referência universal |
| `feriado` | Calendário brasileiro universal |

**Observação:** regras operacionais negociadas com banco (`AceitaLiquidacaoTotal`, `AvisoPrevioMinDiasUteis`) ficam **globais no MVP** desta entrega. Customização por tenant entra em Fase 4 desta iniciativa (caso justifique-se com clientes reais).

### 2.4 Contexto de tenant na requisição

```csharp
public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    bool IsAdmin { get; }
}
```

Resolvido por middleware na pipeline HTTP:

1. Extrai claim `tenant_id` do JWT.
2. Valida que o tenant existe e está ativo (cache em `IMemoryCache` por 5 min).
3. Seta `app.tenant_id` no Postgres via `SET LOCAL app.tenant_id = '...'` no início da transação.
4. Popula `ITenantContext` no scope.

Para `Sgcf.Jobs` (hosted services), o contexto é setado por iteração explícita sobre tenants ativos.

### 2.5 Identificadores de tenant (padrão de mercado)

Decisão sponsor 2026-05-20 — adotar padrão amplamente usado (Stripe, AWS, GitHub, Linear):

- **Chave primária:** `Guid` (UUID v7 quando possível, v4 caso contrário). Identificador canônico interno.
- **Slug:** string kebab-case única (`acme-finance`, `proxys`). Identificador humano usado em URLs admin, logs e operação.
- **Display name:** texto livre exibido na UI (`ACME Finance Ltda.`).

Geração de slug é manual no provisionamento (validado por regex `^[a-z][a-z0-9-]{2,30}$`). Não auto-derivado de nome para evitar colisões.

### 2.6 Hierarquia de roles

Decisão sponsor 2026-05-20 — duas camadas de administração:

| Role | Escopo | Quem |
|------|--------|------|
| `super-admin` | Cross-tenant. Acessa `/admin/*` endpoints, lista todos os tenants, provisiona, suspende, consulta auditoria de qualquer tenant. **Pode impersonar tenant via `X-Tenant-Id`.** | Time interno Nordware (operadores do SaaS) |
| `admin` | Tenant-scoped. Administra usuários, parâmetros, plano de contas, configurações do seu próprio tenant. Não enxerga outros tenants. | Cliente final — gestor administrativo do tenant |
| `gerente`, `tesouraria`, `diretor`, `contabilidade`, `auditor` | Tenant-scoped. Roles operacionais conforme `Policies.cs` atual. | Usuários finais do cliente |

`super-admin` **não substitui** o role `admin` existente — é novo, acima dele. Policies atuais (`Policies.Admin`) renomeiam dependências:

- `Policies.Admin` continua valendo dentro do escopo do tenant (administra parâmetros, etc.).
- `Policies.SuperAdmin` (nova) é usada apenas em `TenantsController`, `AuditoriaAdminController`, healthcheck `/health/rls`.

### 2.7 Endpoints administrativos

Novo controller `TenantsController` em `/api/v1/admin/tenants` requer role `super-admin`. Visível apenas para o time Nordware, nunca para clientes finais.

### 2.8 Cabeçalho `X-Tenant-Id`

**Não usado em endpoints normais.** Tenant vem exclusivamente do JWT. Aceitar header em rotas normais seria vetor de IDOR — usuário poderia tentar consultar dados de outro tenant. Exceção: endpoints `/admin/*` com role `super-admin` podem aceitar `X-Tenant-Id` para impersonação assistida (uso de suporte interno Nordware).

---

## 3. Dependências do Plano

Este plano antecede a Fase 0 do cockpit:

```
Fase −1 (este plano)
    │
    ├── Fundação (Tasks −1.1 a −1.5)
    │       │
    │       ├── Provisionamento (Tasks −1.6, −1.7)
    │       │       │
    │       │       └── Testes de isolação (Task −1.8)
    │       │
    │       └── Configuração por tenant (Task −1.9)
    │
    ▼
Checkpoint C — Fase 0 do cockpit liberada
```

Endpoints do cockpit (Fase 0–3 do `plan_cockpit_backend_gaps.md`) **só começam após Checkpoint C** deste plano.

---

## 4. Tasks

### Fase A — Fundação (Sprints 1 a 2)

#### Task −1.1 — ADR-020 e contrato `ITenantContext`

**Descrição:** Publicar ADR-020 (Estratégia de Multi-Tenancy: Shared Schema + RLS). Definir interface `ITenantContext` + `TenantContext` (DI lifetime `Scoped`) com propriedades `TenantId`, `TenantSlug`, `IsAdmin`. Sem persistência ainda; apenas o contrato.

**Acceptance criteria:**

- [ ] ADR-020 publicado em `docs/adr/`.
- [ ] `ITenantContext` em `Sgcf.Application.Tenancy`.
- [ ] `TenantContext` concreto em `Sgcf.Infrastructure.Tenancy` registrado como `Scoped`.
- [ ] Helper `MissingTenantContextException` para erros explícitos.

**Verification:**

- [ ] `dotnet build` passa.
- [ ] DI smoke test: solicitar `ITenantContext` de um scope retorna instância.

**Dependências:** Nenhuma.

**Files likely touched:**

- `docs/adr/ADR-020-multi-tenancy-shared-schema-rls.md`
- `src/Sgcf.Application/Tenancy/ITenantContext.cs`
- `src/Sgcf.Application/Tenancy/MissingTenantContextException.cs`
- `src/Sgcf.Infrastructure/Tenancy/TenantContext.cs`
- `src/Sgcf.Api/Program.cs` (DI)

**Escopo:** S.

---

#### Task −1.2 — Agregado `Tenant` + repositório + endpoints admin

**Descrição:** Criar agregado `Tenant` em `Sgcf.Domain.Tenancy` com chave primária `Guid` (UUID v7 quando possível) e identificador humano `Slug`. Campos: `Id`, `Slug`, `Nome`, `CnpjMascarado`, `Status`, `PlanoAssinatura`, `CriadoEm`, `SuspensoEm?`. Repositório, migration, controller `TenantsController` em `/api/v1/admin/tenants` com CRUD.

**Acceptance criteria:**

- [ ] Agregado `Tenant` com `Id: Guid` (UUID) + `Slug: string` único.
- [ ] Fábrica `Criar` aceita `slug, nome, cnpj, plano` e gera `Id` internamente.
- [ ] Transições `Suspender`, `Reativar`, `Arquivar`.
- [ ] `Slug` único, kebab-case, validado por regex `^[a-z][a-z0-9-]{2,30}$`.
- [ ] Endpoints CRUD operacionais — `GET/POST /admin/tenants`, `GET/PATCH /admin/tenants/{idOrSlug}`, `POST /admin/tenants/{id}/suspender`, `POST /admin/tenants/{id}/reativar`. Resolver aceita `{idOrSlug}` (UUID ou slug).
- [ ] Autorização: apenas role `super-admin`.
- [ ] AuditLog em todas as mutações.

**Verification:**

- [ ] Integration test: criar, suspender, reativar.
- [ ] Tentativa de criar slug duplicado retorna 409.
- [ ] Usuário sem `super-admin` recebe 403.

**Dependências:** −1.1.

**Files likely touched:**

- `src/Sgcf.Domain/Tenancy/Tenant.cs`
- `src/Sgcf.Domain/Tenancy/StatusTenant.cs`
- `src/Sgcf.Domain/Tenancy/PlanoAssinatura.cs`
- `src/Sgcf.Application/Tenancy/ITenantRepository.cs`
- `src/Sgcf.Application/Tenancy/Commands/*` (Criar, Suspender, Reativar)
- `src/Sgcf.Application/Tenancy/Queries/*`
- `src/Sgcf.Api/Controllers/TenantsController.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AddTenants.cs`
- `tests/Sgcf.Api.IntegrationTests/TenantsControllerTests.cs`

**Escopo:** M.

---

#### Task −1.3 — JWT com claim `tenant_id` + role `super-admin` + middleware resolver

**Descrição:** Atualizar emissão de JWT (dev mock + provider real) para incluir claim `tenant_id` (UUID do tenant) e suportar role `super-admin` para o time Nordware. Implementar `TenantResolverMiddleware`. Adicionar `Policies.SuperAdmin` em `Sgcf.Application.Authorization.Policies`. Dev mock cria tenant `proxys` (slug) com UUID estável no boot.

**Acceptance criteria:**

- [ ] Claim `tenant_id` (UUID) exigida em endpoints não-admin; ausência retorna 401.
- [ ] Claim `roles` inclui `super-admin` para operadores Nordware.
- [ ] `Policies.SuperAdmin` adicionada e usada em `TenantsController` + `AuditoriaAdminController` + `/health/rls`.
- [ ] `Policies.Admin` continua funcional dentro do escopo do tenant (gestão de parâmetros).
- [ ] Tenant suspenso retorna 403 com `detail: "Tenant suspenso"`.
- [ ] Cache de tenants ativos em `IMemoryCache` invalida quando status muda.
- [ ] Dev mock cria tenant `proxys` (slug) + UUID estável + claim `tenant_id` + role `admin` por padrão.
- [ ] Dev mock alternativo cria token com role `super-admin` quando header `X-Dev-Role: super-admin` é informado.
- [ ] Endpoints `/api/v1/admin/*` exigem `super-admin` e podem aceitar `X-Tenant-Id` para impersonação assistida.

**Verification:**

- [ ] Integration test: requisição sem claim retorna 401.
- [ ] Requisição com tenant suspenso retorna 403.
- [ ] Cache invalida em < 1s após `PATCH /admin/tenants/{id}/suspender`.

**Dependências:** −1.1, −1.2.

**Files likely touched:**

- `src/Sgcf.Api/Middleware/TenantResolverMiddleware.cs`
- `src/Sgcf.Api/Program.cs` (registrar middleware após `UseAuthentication`)
- `src/Sgcf.Infrastructure/Tenancy/TenantCache.cs`
- `tests/Sgcf.Api.IntegrationTests/TenantMiddlewareTests.cs`

**Escopo:** M.

---

#### Task −1.4 — Marker `ITenantScoped` + EF Core global query filter

**Descrição:** Interface marker `ITenantScoped { Guid TenantId { get; } }`. Atualizar todas as entidades operacionais (lista em §2.3) para implementar a interface (sem coluna ainda). `SgcfDbContext` aplica global query filter `OnModelCreating` automaticamente via reflection. Atualiza `SaveChangesAsync` para popular `TenantId` em entidades novas a partir do `ITenantContext`.

**Acceptance criteria:**

- [ ] Marker `ITenantScoped` em `Sgcf.Domain.Tenancy`.
- [ ] Todas as ~30 entidades tenant-scoped implementam a interface.
- [ ] `SgcfDbContext.OnModelCreating` adiciona filter automaticamente.
- [ ] `SaveChangesAsync` injeta `TenantId` em `Added` entries.
- [ ] Tentativa de salvar entidade `ITenantScoped` sem `ITenantContext` injetado lança `MissingTenantContextException`.

**Verification:**

- [ ] Unit test: filter aplicado a query lambda gerada para cada DbSet.
- [ ] Integration test: inserir entidade sem TenantContext lança.
- [ ] Quando TenantContext setado para tenant A, queries não retornam dados de tenant B.

**Dependências:** −1.1, −1.3.

**Files likely touched:**

- `src/Sgcf.Domain/Tenancy/ITenantScoped.cs`
- `src/Sgcf.Domain/Contratos/Contrato.cs` (e ~30 outras entidades) — adicionar `Guid TenantId { get; private set; }` + implementar marker
- `src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs` — reflection-based filter
- `src/Sgcf.Infrastructure/Persistence/TenantSaveInterceptor.cs` (popular TenantId em Added entries)
- `tests/Sgcf.Application.Tests/Tenancy/GlobalFilterTests.cs`

**Escopo:** L. **Trabalho repetitivo de propagar `TenantId` em ~30 agregados.**

> **Risco:** se o filter falhar silenciosamente em alguma entidade, vaza dados. Mitigação: teste que itera todos os DbSet e valida que filter foi aplicado.

---

#### Task −1.5 — Migration big-bang: adicionar `tenant_id` + backfill

**Descrição:** Uma única migration `AddTenantIdToAllTables` que:

1. Cria tenant `proxys` em `tenants` (seed).
2. `ALTER TABLE` em todas as tabelas tenant-scoped adicionando `tenant_id UUID NULL`.
3. `UPDATE ... SET tenant_id = '<proxys-uuid>'` em todas as linhas.
4. `ALTER COLUMN tenant_id SET NOT NULL`.
5. Adiciona índices `(tenant_id, ...)` nas chaves naturais existentes.
6. Adiciona FK composite onde aplicável (`parcela.(tenant_id, contrato_id) REFERENCES contrato`).

**Acceptance criteria:**

- [ ] Migration single-file aplica em < 60 s em base de dev (200 contratos).
- [ ] Migration reverte limpando colunas (descarte aceitável em dev).
- [ ] Pós-migração, `SELECT COUNT(*) FROM contrato WHERE tenant_id IS NULL = 0`.
- [ ] Unique constraints existentes reescritas com `(tenant_id, ...)` (ex.: `unique (tenant_id, contrato_id, numero_externo)`).

**Verification:**

- [ ] Aplica em DB de teste (Testcontainers) com seed conhecido.
- [ ] `dotnet test` passa após migration.
- [ ] Tempo de aplicação registrado em log do CI.

**Dependências:** −1.4.

**Files likely touched:**

- `src/Sgcf.Infrastructure/Migrations/<ts>_AddTenantIdToAllTables.cs` (arquivo grande, ~400 linhas SQL)
- `tests/Sgcf.Application.Tests/Migrations/BigBangMigrationTests.cs`

**Escopo:** L.

> **Riscos:** unique constraints podem dar conflito durante o rewrite — testar em base com dados de produção sintéticos. Para produção atual da Proxys, a migration roda em janela de manutenção combinada.

---

### Checkpoint A — Fundação Pronta

- [ ] `dotnet test` verde.
- [ ] EF Core global filter funcionando (teste cross-tenant passa).
- [ ] Tenant `proxys` carregado e operacional.
- [ ] Endpoints existentes do SGCF continuam funcionando com claim `tenant_id` no JWT.
- [ ] Revisão humana antes de Fase B.

---

### Fase B — Provisionamento e Operação (Sprints 3 a 4)

#### Task −1.6 — Provisionamento idempotente de tenant

**Descrição:** Endpoint `POST /api/v1/admin/tenants/{id}/provisionar` que cria os dados-base de um tenant recém criado: parâmetros de sistema (tetão), parâmetros de cotação, plano de contas modelo. Idempotente — repetir não duplica.

**Acceptance criteria:**

- [ ] Provisionamento popula `parametro_sistema` (singleton por tenant).
- [ ] Popula `parametro_cotacao` defaults.
- [ ] Cria plano de contas a partir do modelo global (cópia per-tenant).
- [ ] Idempotência: chamar 2x produz mesmo resultado, sem erros.
- [ ] `AuditLog` registra `TenantProvisionado`.

**Verification:**

- [ ] Integration test: provisionar → consultar → confirmar dados.
- [ ] Provisionar 2x → sem erro, sem duplicação.
- [ ] Tenant suspenso não pode ser provisionado novamente.

**Dependências:** −1.5.

**Files likely touched:**

- `src/Sgcf.Application/Tenancy/Commands/ProvisionarTenantCommand.cs` + Handler
- `src/Sgcf.Application/Tenancy/Services/TenantProvisioner.cs`
- `src/Sgcf.Api/Controllers/TenantsController.cs` (endpoint)
- `tests/Sgcf.Api.IntegrationTests/ProvisionarTenantTests.cs`

**Escopo:** M.

---

#### Task −1.7 — Row Level Security (RLS) policies

**Descrição:** Migration que habilita RLS em todas as tabelas tenant-scoped e cria policies `tenant_isolation`. Usa `current_setting('app.tenant_id', true)::uuid`. Atualizar `SgcfDbContext` para emitir `SET LOCAL app.tenant_id = ...` no início de cada transação via `DbConnectionInterceptor`.

**Acceptance criteria:**

- [ ] Migration habilita RLS + cria policy em todas as ~30 tabelas tenant-scoped.
- [ ] `DbConnectionInterceptor` seta `app.tenant_id` automaticamente no `OpenedAsync`.
- [ ] Conexão sem `app.tenant_id` retorna 0 linhas em qualquer SELECT (validação manual em psql).
- [ ] Role `super-admin` Postgres bypassa RLS (para suporte) — criar role separada.

**Verification:**

- [ ] Teste manual via psql sem `SET app.tenant_id` retorna 0 linhas de `contrato`.
- [ ] Teste com `SET app.tenant_id = 'tenant-a'` retorna apenas dados de A.
- [ ] `dotnet test` continua passando (interceptor cobre).

**Dependências:** −1.5.

**Files likely touched:**

- `src/Sgcf.Infrastructure/Migrations/<ts>_EnableRowLevelSecurity.cs`
- `src/Sgcf.Infrastructure/Persistence/TenantConnectionInterceptor.cs`
- `src/Sgcf.Infrastructure/Persistence/SgcfDbContext.cs` (registrar interceptor)
- `tests/Sgcf.Application.Tests/Tenancy/RlsPoliciesTests.cs`

**Escopo:** M.

> **Risco:** se interceptor falhar, queries não retornam nada. Mitigação: log estruturado de erro + healthcheck que valida RLS habilitado.

---

#### Task −1.8 — Suite de testes de isolação cross-tenant

**Descrição:** Bateria de testes específica que cria dois tenants (A e B), popula dados em cada, e valida que:

1. Query feita no contexto de A nunca retorna dados de B.
2. Tentativa de POST com `tenantId` de B no contexto de A retorna 403 ou 404.
3. Idempotency keys com mesmo valor em tenants diferentes não conflitam.
4. Alertas, AuditLog, todos os agregados respeitam isolação.
5. Listagem paginada não cruza tenants.

Cobre todos os controllers existentes via WebApplicationFactory.

**Acceptance criteria:**

- [ ] 100% dos controllers REST atualmente existentes têm caso de teste cross-tenant.
- [ ] Suite roda em CI com tag `Category=CrossTenantIsolation`.
- [ ] Falha em qualquer teste bloqueia merge.

**Verification:**

- [ ] CI executa a suite e reporta pass/fail por controller.

**Dependências:** −1.6, −1.7.

**Files likely touched:**

- `tests/Sgcf.Api.IntegrationTests/CrossTenantIsolation/*Tests.cs` (~14 arquivos, um por controller)
- `tests/Sgcf.Api.IntegrationTests/Fixtures/MultiTenantFixture.cs`

**Escopo:** L.

---

### Checkpoint B — Isolação Validada

- [ ] Suite cross-tenant 100% verde em CI.
- [ ] RLS habilitada e interceptor funcionando.
- [ ] Provisionamento testado.
- [ ] Sessão de pen-test interno: tentar acessar dados de tenant B com token de tenant A retorna 0 linhas.

---

### Fase C — Configuração Per-Tenant (Sprint 5)

#### Task −1.9 — `ParametroSistema` e `ParametroCotacao` per-tenant

**Descrição:** Refatorar `ParametroSistema` para deixar de ser singleton e virar per-tenant. Tetão mensal vira configuração de cada cliente. Mesmo para `ParametroCotacao` (defaults de IRRF, IOF). Adaptar `ValidadorTetaoMensal` para resolver via `ITenantContext`.

**Acceptance criteria:**

- [ ] `ParametroSistema.Chave = "GLOBAL"` continua existindo mas agora há um registro por tenant (composite key `(tenant_id, chave)`).
- [ ] Endpoint `POST /api/v1/parametros-sistema` aceita configuração do tenant atual.
- [ ] `ValidadorTetaoMensal` lê parâmetro do tenant atual.
- [ ] Migration popula `parametro_sistema` para todos os tenants existentes com defaults.

**Verification:**

- [ ] Test: tenant A com tetão 1mi, tenant B com 5mi — validação aplica corretamente.

**Dependências:** −1.5, −1.6.

**Files likely touched:**

- `src/Sgcf.Domain/Sistema/ParametroSistema.cs` (remover ChaveGlobal)
- `src/Sgcf.Application/Painel/ValidadorTetaoMensal.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_ParametroSistemaPerTenant.cs`
- `tests/Sgcf.Application.Tests/Sistema/ParametroSistemaPerTenantTests.cs`

**Escopo:** M.

---

#### Task −1.10 — Plano de Contas per-tenant

**Descrição:** `PlanoContasGerencial` recebe `TenantId`. Quando tenant é provisionado, cria cópia do plano modelo (global). Posteriormente cada tenant pode customizar.

**Acceptance criteria:**

- [ ] Migration adiciona `tenant_id` a `plano_contas_gerencial`.
- [ ] Provisionamento (Task −1.6) faz cópia do modelo global.
- [ ] Endpoint `GET /api/v1/plano-contas` retorna apenas o do tenant.
- [ ] Edição não afeta outros tenants.

**Verification:**

- [ ] Test: editar conta em tenant A não muda em B.

**Dependências:** −1.5, −1.6.

**Files likely touched:**

- `src/Sgcf.Domain/Contabilidade/PlanoContasGerencial.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_PlanoContasPerTenant.cs`
- `src/Sgcf.Application/Tenancy/Services/TenantProvisioner.cs` (clonagem)

**Escopo:** S.

---

#### Task −1.11 — `AuditLog` per-tenant + endpoints admin cross-tenant

**Descrição:** Adicionar `TenantId` a `AuditLog`. Endpoint `GET /api/v1/admin/auditoria?tenantId=X` para super-admin consultar logs de qualquer tenant. Endpoint normal `GET /api/v1/auditoria` continua filtrado.

**Acceptance criteria:**

- [ ] `AuditLog.TenantId NOT NULL` com migration de backfill.
- [ ] `GET /admin/auditoria?tenantId=X` exige `super-admin`.
- [ ] `GET /auditoria` filtra automaticamente por tenant atual.

**Verification:**

- [ ] Test cross-tenant cobre endpoint admin.

**Dependências:** −1.5.

**Files likely touched:**

- `src/Sgcf.Domain/Auditoria/AuditLog.cs`
- `src/Sgcf.Api/Controllers/AuditoriaController.cs`
- `src/Sgcf.Infrastructure/Migrations/<ts>_AuditLogTenantId.cs`

**Escopo:** S.

---

#### Task −1.12 — Healthcheck `/health/rls` + observabilidade de tenancy

**Descrição:** Endpoint `GET /health/rls` (autorização `Policies.SuperAdmin`) que valida em runtime:

1. RLS está habilitada em todas as tabelas declaradas como tenant-scoped (`pg_class.relrowsecurity`).
2. Cada tabela tenant-scoped tem ao menos uma policy `tenant_isolation` (`pg_policy.polname`).
3. Conexão sem `SET app.tenant_id` retorna 0 linhas em uma tabela canário (`contrato`).
4. Conexão com tenant `proxys` retorna pelo menos uma linha (smoke).

Retorna `200` com `{ status: "healthy", checks: [...] }` ou `503` com diagnóstico detalhado.

Acrescentar métricas: `sgcf_tenant_requests_total{tenant_slug}` (counter), `sgcf_tenant_active_total` (gauge), `sgcf_rls_check_failures_total` (counter, deve ser sempre 0).

**Acceptance criteria:**

- [ ] Endpoint registrado, protegido por `Policies.SuperAdmin`.
- [ ] Quatro checks listados acima implementados.
- [ ] Resposta inclui detalhamento por tabela quando há falha.
- [ ] Métricas Prometheus expostas (se infraestrutura já suporta) ou logs estruturados equivalentes.
- [ ] Documentação em `docs/operacao/multi-tenancy.md` descreve uso e expectativas.

**Verification:**

- [ ] Test: derrubar policy de uma tabela manualmente → endpoint retorna 503 apontando a tabela.
- [ ] Test: endpoint sem `super-admin` retorna 403.

**Dependências:** −1.7.

**Files likely touched:**

- `src/Sgcf.Api/Controllers/HealthController.cs`
- `src/Sgcf.Application/Tenancy/Services/RlsHealthCheckService.cs`
- `docs/operacao/multi-tenancy.md`
- `tests/Sgcf.Api.IntegrationTests/HealthControllerRlsTests.cs`

**Escopo:** S.

---

### Checkpoint C — Pronto para o Cockpit

- [ ] Tasks −1.1 a −1.11 concluídas.
- [ ] Cross-tenant suite verde.
- [ ] RLS habilitada.
- [ ] Demonstração de provisionar 2 tenants e operar ambos em paralelo.
- [ ] Tenant `proxys` (atual produção) opera normalmente.
- [ ] **Fase 0 do `plan_cockpit_backend_gaps.md` desbloqueada.**

---

### Fase D — Adaptações no Cockpit (durante Fases 0 a 3 do plano de cockpit)

Estas não são tasks novas — são ajustes em cada SPEC do cockpit já escrito. Vão ser feitas na vez de cada task original, com adição mínima:

- Cada entidade nova (`Alerta`, `ContaBancaria`, `SaldoCaixa`, `EventoFluxoCaixa`, `DadosContabeisMensal`) implementa `ITenantScoped` desde o nascimento.
- Cada migration inclui `tenant_id NOT NULL` desde a criação.
- Cada endpoint herda filtro automaticamente.
- `Alerta.ChaveIdempotencia` inclui `tenant_id` como prefixo para evitar colisão.
- Rules engine do cockpit (Task 0.4) itera sobre tenants ativos.

**Adicional ao plano do cockpit:** ~10% de esforço em cada task (sem novas tasks).

---

## 5. Parallelization

| Pode rodar em paralelo |
|------------------------|
| −1.6 (provisionamento) e −1.7 (RLS) — depois da Task −1.5 |
| −1.9, −1.10, −1.11, −1.12 — depois do Checkpoint B |

Sequencial obrigatório:

```
−1.1 → −1.2 → −1.3 → −1.4 → −1.5 → Checkpoint A
                                       │
                                       ├── −1.6
                                       └── −1.7
                                            │
                                            └── −1.8 → Checkpoint B
                                                         │
                                                         ├── −1.9
                                                         ├── −1.10
                                                         ├── −1.11
                                                         └── −1.12 → Checkpoint C
```

---

## 6. Riscos e Mitigações

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Filter EF Core não aplicado em alguma entidade nova → vazamento cross-tenant | Crítico (LGPD) | Reflection automática em `OnModelCreating` + RLS como segunda camada + suite cross-tenant em CI |
| Migration big-bang (−1.5) demora muito em produção real | Alto | Testar em base sintética de 1 000 contratos antes; planejar janela de manutenção de 30 min |
| Unique constraints existentes conflitam com novos `(tenant_id, ...)` | Médio | Dry-run em base de dev com dados de produção espelhados |
| RLS adiciona overhead de query | Baixo | Índice `(tenant_id, ...)` em todas as tabelas; benchmark P95 antes/depois |
| Hosted services em `Sgcf.Jobs` esquecem de setar `app.tenant_id` | Alto | Helper `WithTenantScopeAsync` + lint check no PR (analisador customizado) |
| MCP/A2A precisam carregar tenant em prompts | Médio | Adicionar parâmetro `tenant_id` obrigatório nas tools; doc no `Sgcf.Mcp/CLAUDE.md`. *Sem clientes externos hoje (decisão sponsor) — sem janela de coexistência necessária.* |
| Exportação consolidada (admin) precisa cross-tenant | Médio | Helper `WithTenantBypassAsync` para super-admin; uso explícito e auditado |
| Idempotency Key colide entre tenants | Médio | Adicionar `tenant_id` como prefixo nos `IdempotencyFilter` |

---

## 7. Itens Fora do Escopo Desta Entrega

Tratados em Fase 4+ futura, conforme demanda:

- **Schema/DB por tenant** (clientes enterprise) — apenas se cliente real pedir.
- **Data residency** (tenant em outra região GCP).
- **Regras operacionais de banco por tenant** (`Banco.AceitaLiquidacaoTotal` override).
- **SSO/SAML por tenant** — autenticação federada.
- **Subdomínios por tenant** (`acme.sgcf.app`).
- **Faturamento por uso** (medição de contratos/cotações por tenant).
- **Sandbox por tenant** (ambiente de teste isolado).

---

## 8. Decisões do Sponsor — 2026-05-20

1. **Identificação de tenant:** padrão de mercado — `Guid` PK (UUID v7 preferencial) + `Slug` kebab-case humano. Documentado em §2.5.
2. **Backup/restore:** fora do escopo agora. Estratégia (dump completo vs por tenant) revisitada quando houver SLA contratual com primeiro cliente externo. Possivelmente os dois modos depois.
3. **MCP/A2A:** sistema ainda não lançado, sem clientes externos. Sem janela de coexistência necessária. Tools recebem `tenant_id` obrigatório desde o dia 1 da multi-tenancy.
4. **Roles:** `super-admin` exclusivo do time interno Nordware. Demais usuários operam com `admin` (escopo do tenant) ou roles operacionais existentes. Hierarquia documentada em §2.6.
5. **Healthcheck `/health/rls`:** confirmado. Implementado em Task −1.12.

## 9. Decisões adicionais — 2026-05-20

6. **Invalidação de cache de tenants:** Redis pub/sub com TTL local de 5 min como rede de segurança. Justificativa: Redis já é dependência, latência sub-segundo crítica para suspensão de tenants comprometidos, TTL cobre raras perdas de mensagem. Aplicado em Task −1.3.
7. **Visibilidade de impersonação:** transparência total e obrigatória — `Impersonating` e `ImpersonatedBy` sempre expostos ao admin do tenant via `GET /auditoria?impersonating=true`. Sem flag de ocultamento. Justificativa: LGPD (princípio da transparência), trust ao cliente, anti-abuso interno. Aplicado em Task −1.11.
8. **Branch protection:** ativada em `main`. Status checks obrigatórios incluem `Cross-Tenant Isolation Tests`. `CODEOWNERS` exige revisão de security-team em paths críticos de tenancy. Workflow YAML, CODEOWNERS e runbook entregues junto com Task −1.8. Configuração no GitHub feita pelo administrador conforme `docs/operacao/ci-branch-protection.md`.

## 10. Refinamentos remanescentes

Nenhum bloqueante. Todos os refinamentos foram resolvidos.

---

## 11. Critério de Pronto para o Cockpit

Multi-tenancy é considerada entregue quando:

- [ ] Checkpoint C atingido.
- [ ] Tenant `proxys` (atual) opera normalmente sem regressão funcional.
- [ ] É possível criar tenant `cliente-acme` via endpoint admin, provisionar, e operar em paralelo.
- [ ] Suite cross-tenant verde em CI.
- [ ] ADR-020 publicado.
- [ ] Documentação operacional (`docs/operacao/multi-tenancy.md`) descreve onboarding, suspensão, troubleshooting.

---

## 12. Referências

- `tasks/plan_cockpit_backend_gaps.md` v1.1 — depende deste plano.
- `sgcf-backend/CLAUDE.md` — regras inegociáveis aplicáveis.
- `docs/adr/ADR-020-multi-tenancy-shared-schema-rls.md` (a criar em Task −1.1).
- PostgreSQL RLS docs: https://www.postgresql.org/docs/16/ddl-rowsecurity.html
- EF Core global query filters: https://learn.microsoft.com/en-us/ef/core/querying/filters

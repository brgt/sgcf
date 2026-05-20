# ADR-020 — Multi-Tenancy: Shared Schema + tenant_id + Postgres RLS

| Campo    | Valor                         |
|----------|-------------------------------|
| Status   | Aceito                        |
| Data     | 2026-05-20                    |
| Autor    | Time de Arquitetura           |
| Sponsor  | Welysson Soares               |

---

## Contexto

O SGCF precisa suportar múltiplos clientes (tenants) de forma isolada. A decisão de arquitetura
multi-tenancy afeta custo operacional, complexidade de migrations, isolamento de dados e
superfície de ataque.

---

## Decisão

**Shared Schema + tenant_id UUID NOT NULL + Postgres RLS (Row-Level Security).**

- ~30 tabelas de negócio recebem coluna `tenant_id UUID NOT NULL`.
- **EF Core Global Query Filter** é a primeira camada de isolamento (aplicada automaticamente
  via `ITenantScoped` marker interface).
- **Postgres RLS** é a segunda camada de segurança (net de proteção contra bugs de código que
  ignorem o query filter via `IgnoreQueryFilters()`).
- Catálogos globais (`banco`, `feriado`, `cotacao_fx`, `cdi_snapshot`) não recebem `tenant_id`
  — são dados de referência compartilhados.

---

## Identificação de Tenants

- **ID:** `Guid` (UUID v7) — PK surrogate; usado internamente e nas FKs.
- **Slug:** string kebab-case `[a-z][a-z0-9-]{2,30}` — usado em URLs, logs e cache keys.
- Ambos são únicos e imutáveis após criação.

---

## Hierarquia de Roles

```
super-admin (Nordware — cross-tenant)
  └── admin (tenant-scoped — administra seu próprio tenant)
        └── demais roles (tesouraria, gerente, diretor, contabilidade, auditor)
```

- Super-admin pode impersonar qualquer tenant via header `X-Tenant-Id`.
- Admin é scoped ao seu tenant — não enxerga dados de outros tenants.

---

## Fluxo de Resolução (por Request)

```
Request chega
    ↓
UseAuthentication() — valida JWT, popula ClaimsPrincipal
    ↓
TenantResolverMiddleware
    ├── Paths de bypass: /api/v1/admin, /health, /swagger → skip
    ├── Extrai tenant_id do claim JWT
    ├── Super-admin: aceita X-Tenant-Id header (impersonation)
    ├── Consulta TenantCache (MemoryCache 5min + Redis pub/sub invalidation)
    ├── Tenant suspenso/arquivado → 403
    └── tenantContext.Resolve(id, slug, isSuperAdmin, isImpersonating)
    ↓
UseAuthorization() — verifica policies (Leitura, Escrita, SuperAdmin…)
    ↓
Handler — ITenantContext já resolvido; EF global filter ativo
```

---

## Alternativas Descartadas

| Alternativa                | Motivo da recusa                                                      |
|----------------------------|-----------------------------------------------------------------------|
| Database por tenant        | Custo alto (N databases, N connection pools, migrations N×)           |
| Schema por tenant          | Migrations 10× mais caras; Npgsql não suporta schema-per-tenant bem   |
| Só EF Global Query Filter  | Sem rede de segurança; bug de código expõe dados cross-tenant         |
| Só Postgres RLS            | Quebra a abstração EF; força SQL raw em todos os queries              |
| Hybrid (schema + shared)   | Complexidade sem benefício proporcional dado o porte atual do produto |

---

## Consequências

- **Positivo:** Operação simples — um único banco, uma migration por feature.
- **Positivo:** EF query filter + RLS = defesa em profundidade.
- **Positivo:** Backfill de dados históricos viável (big-bang migration Task -1.5).
- **Negativo:** Tenant isolation não é absoluta em nível de sistema operacional
  (mitigado por RLS + auditoria).
- **Negativo:** Unique constraints devem ser compostas com `tenant_id` (exemplo:
  `contrato.numero_externo` → `(tenant_id, numero_externo)`).

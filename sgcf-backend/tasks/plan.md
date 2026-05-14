# SGCF Frontend — Implementation Plan

**Stack:** Vue 3 (latest) + TypeScript strict + Vite + Pinia + TanStack Query + Vue Router + VeeValidate/Zod + Axios + Nordware Design System
**Repo layout:** Separate Vite SPA in sibling folder `sgcf-frontend/` (separate from `sgcf-backend/`)
**Backend baseline:** Sgcf.Api at `http://localhost:5000`, 27 endpoints across 8 controllers (already running)
**Auth in v1:** Dev token from `.env`, hardcoded role claims for local testing. Real OIDC deferred to Phase 5.
**Scope:** All 8 controllers in v1 (Bancos, Contratos, Hedges, Painel, ParametrosCotacao, PlanoContas, Simulador, EBITDA).

---

## 1. Architectural Decisions

| # | Decision | Rationale |
|---|---|---|
| AD-01 | Separate Vite SPA (`sgcf-frontend/`) | Clean separation, independent deploys, no CORS complications in dev (Vite proxy) |
| AD-02 | Pinia for client state + TanStack Query (Vue Query) for server state | Server cache, auto-invalidation, optimistic updates, refetch-on-window-focus out of the box. Pinia only holds UI state (filters, modals, auth user). |
| AD-03 | Server-side pagination + filter on Contratos list (backend extension required) | Contracts list will grow past 500 — client-side will not scale. Backend gets `ListContratosQuery(filter, page, pageSize, sort)`. |
| AD-04 | VeeValidate + Zod for forms | Type-safe, schema co-located with form, plays well with Nordware `Input` `error` prop |
| AD-05 | Axios with interceptors | Auth header injection + global 401/403 handler + correlation ID forwarding |
| AD-06 | vue-i18n (PT-BR only initially, architected for multi-lang) | All UI strings centralized; future EN/ES with no refactor |
| AD-07 | Mock auth in v1 via `.env`: VITE_DEV_TOKEN + VITE_DEV_ROLES | Skip OIDC integration; allows role-based UI testing today |
| AD-08 | Money rendering uses backend's 6-decimal precision, displays at 2 decimals (BRL locale) | Match backend's `Math.Round(x, 6, AwayFromZero)` semantics; never mutate decimals client-side |
| AD-09 | Dates: ISO 8601 strings on the wire, `@js-joda/core` client-side | Backend uses NodaTime LocalDate (`yyyy-MM-dd`) and Instant (`yyyy-MM-ddTHH:mm:ss.fffffffZ`). js-joda mirrors NodaTime's API in JS. |
| AD-10 | Role-gated UI via `<RoleGate>` wrapper + `useAuth().hasPolicy(...)` composable | Hide buttons/menu items for unauthorized roles; backend remains the source of truth |
| AD-11 | URL is single source of truth for list state (filters, page, sort) | Bookmarkable/shareable views; back/forward works; refresh preserves state |
| AD-12 | Forms use a stepper pattern for Contract creation (5 modality flavors are big) | Reduce cognitive load; allow save-as-draft later; clear progress feedback |

---

## 2. Tech Stack (locked)

```
Build:        Vite 5 + TypeScript 5 (strict + noUncheckedIndexedAccess)
Framework:    Vue 3.5+
Router:       Vue Router 4
State:        Pinia 2 (UI), TanStack Query @tanstack/vue-query 5 (server)
HTTP:         Axios 1.x
Forms:        VeeValidate 4 + @vee-validate/zod + Zod 3
UI:           @nordware/design-system (local symlink/file dep) + UnoCSS
Icons:        i-carbon-* + i-mdi-* (matches Nordware DS)
Dates:        @js-joda/core + @js-joda/timezone
Numbers:      Intl.NumberFormat (no extra dep)
i18n:         vue-i18n 10 (PT-BR)
Test:         Vitest 2 + @vue/test-utils + Playwright (e2e in Phase 4)
Lint:         ESLint 9 + Prettier + @typescript-eslint/strict
```

---

## 3. Project Structure

```
sgcf-frontend/
├── public/
├── src/
│   ├── app/                          # App bootstrap, router, providers
│   │   ├── main.ts
│   │   ├── App.vue
│   │   ├── router.ts                 # Vue Router with role-guarded routes
│   │   └── providers/
│   │       ├── query.ts              # TanStack Query setup
│   │       └── i18n.ts
│   │
│   ├── shared/                       # Cross-cutting infrastructure
│   │   ├── api/
│   │   │   ├── client.ts             # Axios instance + interceptors
│   │   │   ├── types.ts              # Generated/written DTOs (mirror C#)
│   │   │   └── endpoints.ts          # URL constants
│   │   ├── auth/
│   │   │   ├── useAuth.ts            # Composable: user, roles, hasPolicy()
│   │   │   ├── RoleGate.vue          # Conditional rendering by policy
│   │   │   └── policies.ts           # Mirror of backend Policies.cs
│   │   ├── money/
│   │   │   ├── Money.ts              # { valor: Decimal, moeda: Moeda }
│   │   │   ├── formatMoney.ts        # Intl.NumberFormat per Moeda
│   │   │   └── parseMoney.ts         # User input → Decimal
│   │   ├── dates/
│   │   │   ├── formatDate.ts         # LocalDate → 'dd/MM/yyyy'
│   │   │   ├── formatInstant.ts      # Instant → 'dd/MM/yyyy HH:mm'
│   │   │   └── BR_TIMEZONE.ts        # 'America/Sao_Paulo'
│   │   ├── filters/                  # ★ Generic URL-driven filter system
│   │   │   ├── useUrlFilters.ts      # composable: filters ↔ URL query
│   │   │   ├── usePagedList.ts       # composable: filter+page+sort → fetch
│   │   │   └── FilterBar.vue         # Slot-based filter UI shell
│   │   ├── forms/
│   │   │   ├── useForm.ts            # VeeValidate wrapper
│   │   │   └── zodSchemas/           # Shared schemas (Money, LocalDate, etc.)
│   │   ├── ui/                       # App-level UI primitives (extend Nordware DS)
│   │   │   ├── ConfirmDialog.vue
│   │   │   ├── ErrorBoundary.vue
│   │   │   └── LoadingState.vue
│   │   └── i18n/
│   │       └── locales/pt-BR.json
│   │
│   ├── features/                     # ★ Feature folders, one per controller
│   │   ├── bancos/
│   │   │   ├── pages/
│   │   │   │   ├── BancosListPage.vue
│   │   │   │   ├── BancoDetailPage.vue
│   │   │   │   └── BancoFormPage.vue       # create + config-antecipacao
│   │   │   ├── components/
│   │   │   │   ├── BancoCard.vue
│   │   │   │   └── BancoFiltersBar.vue
│   │   │   ├── api/
│   │   │   │   ├── bancosApi.ts            # raw fetch fns
│   │   │   │   └── useBancosQueries.ts     # TanStack Query hooks
│   │   │   ├── stores/
│   │   │   │   └── useBancosFiltersStore.ts
│   │   │   └── schemas/
│   │   │       └── bancoFormSchema.ts
│   │   │
│   │   ├── contratos/                # ★ Most complex feature
│   │   │   ├── pages/
│   │   │   │   ├── ContratosListPage.vue   # List + filters + paging
│   │   │   │   ├── ContratoDetailPage.vue  # Tabs: Resumo, Cronograma, Garantias, Hedges
│   │   │   │   ├── ContratoCreatePage.vue  # Stepper (modality-aware)
│   │   │   │   └── ContratoSimularPage.vue # Antecipação simulation
│   │   │   ├── components/
│   │   │   │   ├── ContratosFiltersBar.vue
│   │   │   │   ├── ContratoStatusBadge.vue
│   │   │   │   ├── steps/
│   │   │   │   │   ├── Step1_DadosBasicos.vue
│   │   │   │   │   ├── Step2_Modalidade.vue       # branches per ModalidadeContrato
│   │   │   │   │   ├── Step3_TaxaPrazo.vue
│   │   │   │   │   ├── Step4_Garantias.vue        # optional add-now
│   │   │   │   │   └── Step5_Revisao.vue
│   │   │   │   ├── modalityForms/
│   │   │   │   │   ├── FinimpForm.vue
│   │   │   │   │   ├── Lei4131Form.vue
│   │   │   │   │   ├── RefinimpForm.vue           # contrato-pai selector
│   │   │   │   │   ├── NceForm.vue
│   │   │   │   │   ├── BalcaoCaixaForm.vue
│   │   │   │   │   └── FgiForm.vue
│   │   │   │   ├── CronogramaTable.vue            # parcelas list (sortable)
│   │   │   │   ├── GarantiasPanel.vue
│   │   │   │   └── SimulacaoResultadoCard.vue
│   │   │   ├── api/
│   │   │   ├── stores/
│   │   │   └── schemas/                            # zod schemas per modality
│   │   │
│   │   ├── garantias/                # nested under contratos but own forms
│   │   │   ├── forms/                  # 8 guarantee-type forms (CDB, SBLC, Aval, etc.)
│   │   │   ├── components/GarantiaTypeBadge.vue
│   │   │   └── pages/                  # accessed via ContratoDetailPage
│   │   │
│   │   ├── hedges/
│   │   ├── painel/                   # 4 dashboards
│   │   │   ├── pages/
│   │   │   │   ├── PainelDividaPage.vue
│   │   │   │   ├── PainelGarantiasPage.vue
│   │   │   │   ├── CalendarioVencimentosPage.vue
│   │   │   │   └── KpisExecutivoPage.vue
│   │   │   └── components/
│   │   │       ├── DividaPorMoedaCard.vue
│   │   │       ├── AjusteMtmCard.vue
│   │   │       ├── CalendarioMesGrid.vue
│   │   │       └── EbitdaInputForm.vue
│   │   │
│   │   ├── simulador/
│   │   │   ├── pages/
│   │   │   │   ├── CenarioCambialPage.vue
│   │   │   │   └── AntecipacaoPortfolioPage.vue
│   │   │   └── components/CenarioCard.vue
│   │   │
│   │   ├── plano-contas/
│   │   └── parametros-cotacao/
│   │
│   └── layouts/
│       ├── AppShell.vue              # Sidebar + topbar + main slot
│       ├── PublicLayout.vue          # For login (placeholder until OIDC)
│       └── SidebarNav.vue            # Role-aware menu
│
├── tests/
│   ├── unit/                         # *.spec.ts colocated with code under src/
│   └── e2e/                          # Playwright
│
├── .env.example
├── .env.development                  # VITE_API_BASE_URL=http://localhost:5000, VITE_DEV_TOKEN=..., VITE_DEV_ROLES=tesouraria,admin
├── index.html
├── package.json
├── tsconfig.json
├── uno.config.ts                     # Re-uses Nordware DS UnoCSS config
└── vite.config.ts                    # + proxy /api → http://localhost:5000
```

---

## 4. ★ Filter System Design (Contracts list — most critical)

### 4.1 User-facing filter UX (Contratos list)

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Contratos                                                  [+ Novo]      │
├──────────────────────────────────────────────────────────────────────────┤
│ ┌─Busca─────────────┐ ┌─Banco──────┐ ┌─Modalidade─┐ ┌─Moeda─┐ ┌─Status─┐ │
│ │ 🔍 nº ext/interno│ │ Todos   ▾ │ │ Todas   ▾ │ │ Todas ▾│ │Ativos▾│ │
│ └───────────────────┘ └────────────┘ └────────────┘ └────────┘ └────────┘ │
│ ┌─Vencimento de─┐ ┌─Vencimento até─┐ ┌─Faixa de valor (BRL)──────────┐ │
│ │ dd/mm/yyyy   │ │ dd/mm/yyyy    │ │ De [____] Até [____]         │ │
│ └───────────────┘ └────────────────┘ └───────────────────────────────┘ │
│ ┌─Filtros avançados ▾─────────────────────────────────────────────────┐ │
│ │ □ Com hedge   □ Sem hedge   □ Com garantia   □ Sem garantia        │ │
│ │ □ Tem MarketFlex   □ Tem alerta de vencimento próximo              │ │
│ └─────────────────────────────────────────────────────────────────────┘ │
│ [Limpar filtros]                              Salvar visão como preset ★│
├──────────────────────────────────────────────────────────────────────────┤
│ Filtros ativos: [Banco: Bradesco ✕] [Modalidade: Finimp ✕] [Status:...] │
├──────────────────────────────────────────────────────────────────────────┤
│ Mostrando 47 de 312 contratos · 25 por página · Ordenar por: Vencim. ↑ │
│ ┌────────────────────────────────────────────────────────────────────┐ │
│ │ DataTable (Nordware DS)                                            │ │
│ │  Nº Ext │ Cód Int │ Banco │ Modal. │ Moeda │ Princ. │ Venc. │ ⚙  │ │
│ └────────────────────────────────────────────────────────────────────┘ │
│  ◀ 1 2 3 ... 13 ▶                                                       │
└──────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Filter contract (URL ↔ backend)

URL query mirror:
```
/contratos?q=ABC123&bancoId=<uuid>&modalidade=Finimp&moeda=USD&status=Ativo
          &vencDe=2026-06-01&vencAte=2026-12-31
          &valorMin=100000&valorMax=5000000
          &temHedge=true&temGarantia=true&temAlerta=true
          &page=2&pageSize=25&sort=dataVencimento&dir=asc
```

Backend extension (Phase 0 task):
```csharp
public sealed record ListContratosQuery(
    string? Search,                     // matches NumeroExterno OR CodigoInterno (ILIKE)
    Guid? BancoId,
    ModalidadeContrato? Modalidade,
    Moeda? Moeda,
    StatusContrato? Status,
    LocalDate? DataVencimentoDe,
    LocalDate? DataVencimentoAte,
    decimal? ValorPrincipalMin,         // converted to BRL via PTAX D-1 for comparison
    decimal? ValorPrincipalMax,
    bool? TemHedge,                     // requires JOIN to Hedge
    bool? TemGarantia,                  // requires JOIN to Garantia
    bool? TemAlertaVencimento,          // requires JOIN to AlertaVencimento
    int Page = 1,
    int PageSize = 25,
    string Sort = "DataVencimento",
    string Dir = "asc"
) : IRequest<PagedResult<ContratoDto>>;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
```

### 4.3 `useUrlFilters` composable (frontend)

```ts
// shared/filters/useUrlFilters.ts
export function useUrlFilters<TFilters extends Record<string, unknown>>(
  schema: ZodSchema<TFilters>,
  defaults: TFilters,
) {
  const route = useRoute()
  const router = useRouter()

  // Parse from query string with zod (handles type coercion + validation)
  const filters = computed<TFilters>(() => schema.parse({ ...defaults, ...route.query }))

  // Patch URL (replace, not push, while typing search)
  const setFilters = (patch: Partial<TFilters>, mode: 'push' | 'replace' = 'replace') => {
    const next = { ...filters.value, ...patch }
    router[mode]({ query: omitDefaults(next, defaults) })
  }

  const resetFilters = () => router.replace({ query: {} })

  return { filters, setFilters, resetFilters }
}
```

### 4.4 `usePagedList` composable (combines filters + TanStack Query)

```ts
export function usePagedList<TFilters, TItem>(opts: {
  queryKey: readonly unknown[]
  fetchFn: (f: TFilters) => Promise<PagedResult<TItem>>
  filters: Ref<TFilters>
  debounceMs?: number          // for search inputs
}) {
  const debounced = refDebounced(opts.filters, opts.debounceMs ?? 300)
  return useQuery({
    queryKey: computed(() => [...opts.queryKey, unref(debounced)] as const),
    queryFn: () => opts.fetchFn(unref(debounced)),
    placeholderData: keepPreviousData,    // smooth pagination
  })
}
```

### 4.5 Behaviors

- **Debounced search** (300 ms) on `q` text input only — all other filters apply instantly
- **Active filter chips** below the bar; each is dismissible
- **URL is source of truth** — paste a URL with filters and the page renders identically
- **Filter presets** (later phase): name + save current URL query into localStorage
- **Empty state** when zero results: shows current filter summary + "Limpar filtros" CTA
- **Loading state** uses `keepPreviousData` so the table doesn't flicker between pages

---

## 5. ★ CRUD Pattern (uniform across all 8 features)

### 5.1 Read

```ts
// features/bancos/api/useBancosQueries.ts
export const bancosQueryKeys = {
  all: ['bancos'] as const,
  list: (filters: BancoFilters) => [...bancosQueryKeys.all, 'list', filters] as const,
  detail: (id: string) => [...bancosQueryKeys.all, 'detail', id] as const,
}

export const useBancosList = (filters: Ref<BancoFilters>) =>
  useQuery({
    queryKey: computed(() => bancosQueryKeys.list(unref(filters))),
    queryFn: () => bancosApi.list(unref(filters)),
  })

export const useBancoDetail = (id: Ref<string>) =>
  useQuery({
    queryKey: computed(() => bancosQueryKeys.detail(unref(id))),
    queryFn: () => bancosApi.get(unref(id)),
    enabled: computed(() => !!unref(id)),
  })
```

### 5.2 Create / Update / Delete (optimistic + invalidation)

```ts
export const useCreateBanco = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: bancosApi.create,
    onSuccess: (newBanco) => {
      qc.invalidateQueries({ queryKey: bancosQueryKeys.all })
      qc.setQueryData(bancosQueryKeys.detail(newBanco.id), newBanco)
      toast.success('Banco criado')
    },
    onError: (err) => toast.error(extractApiError(err)),
  })
}

export const useUpdateBancoConfig = () => {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: bancosApi.updateConfig,
    onMutate: async ({ id, patch }) => {
      // Optimistic
      await qc.cancelQueries({ queryKey: bancosQueryKeys.detail(id) })
      const previous = qc.getQueryData<BancoDto>(bancosQueryKeys.detail(id))
      qc.setQueryData(bancosQueryKeys.detail(id), { ...previous, ...patch })
      return { previous }
    },
    onError: (err, vars, ctx) => {
      if (ctx?.previous) qc.setQueryData(bancosQueryKeys.detail(vars.id), ctx.previous)
      toast.error(extractApiError(err))
    },
    onSettled: (data, _err, vars) => {
      qc.invalidateQueries({ queryKey: bancosQueryKeys.detail(vars.id) })
      qc.invalidateQueries({ queryKey: [...bancosQueryKeys.all, 'list'] })
    },
  })
}

export const useDeleteContrato = () => {
  // Same pattern; confirmation dialog before mutating
}
```

### 5.3 Form pattern (VeeValidate + Zod)

```vue
<script setup lang="ts">
const schema = toTypedSchema(bancoCreateSchema)
const { handleSubmit, errors, defineField } = useForm({ validationSchema: schema })

const [codigoCompe] = defineField('codigoCompe')
const [razaoSocial] = defineField('razaoSocial')

const mutation = useCreateBanco()
const onSubmit = handleSubmit((vals) => mutation.mutate(vals, { onSuccess: () => router.push('/bancos') }))
</script>

<template>
  <form @submit.prevent="onSubmit">
    <Input v-model="codigoCompe" label="Código Compe" :error="errors.codigoCompe" required />
    <Input v-model="razaoSocial" label="Razão Social" :error="errors.razaoSocial" required />
    <!-- ... -->
    <Button type="submit" :loading="mutation.isPending.value">Criar</Button>
  </form>
</template>
```

### 5.4 Contract creation flow (5-step stepper)

```
Step 1: Dados Básicos      → numeroExterno, codigoInterno?, bancoId, moeda
Step 2: Modalidade          → modalidade (radio) → reveals modality-specific subform
                              • Finimp:       FinimpForm (ROF, exportador, marketFlex, ...)
                              • Lei4131:      Lei4131Form (SBLC, marketFlex, ...)
                              • Refinimp:     RefinimpForm (contratoPaiId, % refinanciado)
                              • Nce:          NceForm
                              • BalcaoCaixa:  BalcaoCaixaForm
                              • Fgi:          FgiForm
Step 3: Taxa & Prazo        → valorPrincipal, taxaAa, baseCalculo, dataContratacao, dataVencimento
Step 4: Garantias           → optional add 0..N garantias via type-specific forms
Step 5: Revisão & Confirmar → read-only summary → POST + idempotency key
```

After successful POST → redirect to `ContratoDetailPage(/:id)` with toast "Contrato criado".

---

## 6. Routing Map

```
/                                  → redirect /painel (if authed) | /login
/login                             → placeholder (mock auth in v1)
/painel                            → PainelDividaPage (default tab)
/painel/garantias                  → PainelGarantiasPage
/painel/vencimentos                → CalendarioVencimentosPage
/painel/kpis                       → KpisExecutivoPage    [policy: Executivo]
/painel/ebitda                     → EbitdaInputPage      [policy: Auditoria]

/contratos                         → ContratosListPage    (filters in URL query)
/contratos/novo                    → ContratoCreatePage   [policy: Escrita]
/contratos/:id                     → ContratoDetailPage   (tabs: Resumo/Cronograma/Garantias/Hedges/Simulação)
/contratos/:id/simular             → ContratoSimularPage  (antecipação form + result)
/contratos/:id/garantias/nova      → modal-as-route
/contratos/:id/hedges/novo         → modal-as-route

/bancos                            → BancosListPage
/bancos/novo                       → BancoFormPage        [policy: Admin]
/bancos/:id                        → BancoDetailPage
/bancos/:id/editar-config          → BancoFormPage        [policy: Admin]

/hedges                            → HedgesListPage
/hedges/:id                        → HedgeDetailPage

/simulador/cenario-cambial         → CenarioCambialPage   [policy: Executivo]
/simulador/antecipacao-portfolio   → AntecipacaoPortfolioPage [policy: Executivo]

/plano-contas                      → PlanoContasListPage
/plano-contas/novo                 → PlanoContasFormPage  [policy: Auditoria]
/plano-contas/:id/editar           → PlanoContasFormPage  [policy: Auditoria]

/parametros-cotacao                → ParametrosListPage   [policy: Admin]
/parametros-cotacao/novo           → ParametroFormPage    [policy: Admin]
/parametros-cotacao/:id/editar     → ParametroFormPage    [policy: Admin]

/*                                 → NotFoundPage
```

Role enforcement: `router.beforeEach` checks `to.meta.policy` against `useAuth().hasPolicy(...)`. 403 page if forbidden.

---

## 7. Dependency Graph

```
                          [Backend Sgcf.Api running]
                                     │
                                     ▼
                            [0.1 Backend filter+paging
                              extensions]
                                     │
                                     ▼
            ┌────────[0.2 Frontend scaffold (Vite + DS + router)]────────┐
            │                                                            │
            ▼                                                            ▼
   [0.3 Auth mock + RoleGate]                            [0.4 API client + types + i18n]
            │                                                            │
            └────────────────────────────┬───────────────────────────────┘
                                         ▼
                        [0.5 Shared composables: useUrlFilters, usePagedList,
                          Money, Date, ConfirmDialog, ErrorBoundary]
                                         │
                                         ▼
                              [0.6 AppShell + SidebarNav + topbar]
                                         │
            ┌──────────────────┬─────────┴────────┬──────────────────┐
            ▼                  ▼                  ▼                  ▼
        Bancos             Contratos          Painel            Plano de Contas
        (CRUD)             (★ biggest)        (4 dashboards)    (CRUD)
                               │
                               ├── Modality forms (6)
                               ├── Cronograma
                               ├── Garantias (8 types) ─── Garantias forms
                               ├── Hedges (nested)     ─── Hedges feature
                               └── Simulação Antecipação
            ┌──────────────────────────────────────────────────────────┐
            ▼                  ▼                  ▼
        Simulador          Parâmetros       EBITDA upsert
                                                        │
                                                        ▼
                                              [4.x Polish + E2E + Build]
```

---

## 8. Phase Breakdown

> All 8 controllers ship together (per user decision). Phases are internal milestones, not separate releases. Each phase ends with a **checkpoint** that must be green before continuing.

### Phase 0 — Foundation (week 1)

Backend & frontend scaffolding. **Both repos changed.**

- **0.1** Backend: Extend `ListContratosQuery` with filters + paging + sort; add `PagedResult<T>`; add equivalent for `ListBancosQuery` and `ListPlanoContasQuery`
- **0.2** Frontend: Create `sgcf-frontend/` with Vite + Vue 3 + TS strict + Pinia + Vue Query + Router + UnoCSS + ESLint/Prettier
- **0.3** Frontend: Wire Nordware DS as file-protocol dependency; verify `Button`, `Input`, `DataTable`, `Modal`, `PageLayout` render in a smoke page
- **0.4** Frontend: API client with Axios + interceptors (auth header, 401 redirect, error envelope parser); env-driven `VITE_API_BASE_URL`
- **0.5** Frontend: Mock auth (`useAuth`) reading `VITE_DEV_TOKEN` and `VITE_DEV_ROLES` + `RoleGate.vue` + `policies.ts`
- **0.6** Frontend: TypeScript DTOs mirroring all C# DTOs (`shared/api/types.ts`) + enum constants
- **0.7** Frontend: Shared composables (`useUrlFilters`, `usePagedList`), Money/Date formatters, ConfirmDialog, ErrorBoundary
- **0.8** Frontend: `AppShell` layout with sidebar (role-aware menu) + topbar (user + logout placeholder) + main router-view

**Checkpoint 0** (≈end of week 1):
- [ ] Backend `GET /api/v1/contratos?bancoId=...&page=1&pageSize=25` returns `PagedResult<ContratoDto>` with valid filtering
- [ ] Backend tests pass for the new query (unit + integration)
- [ ] Frontend dev server boots; `/` shows AppShell with empty router-view
- [ ] Smoke page renders 4 DS components correctly
- [ ] Auth gate redirects unauthenticated → `/login`; sets mock user when `VITE_DEV_TOKEN` present
- [ ] ESLint, type-check, unit tests all green

### Phase 1 — Contratos (★ flagship feature; weeks 2-3)

The biggest, most complex feature. Slice vertically per CRUD operation.

- **1.1** ContratosListPage with filters + paging (uses backend extension from 0.1)
- **1.2** Active filter chips + URL persistence + empty/loading states
- **1.3** ContratoDetailPage with tabs (Resumo / Cronograma / Garantias / Hedges / Simulação)
- **1.4** ContratoCreatePage — Step 1 (Dados Básicos) + Step 3 (Taxa & Prazo) + Step 5 (Revisão)
- **1.5** ContratoCreatePage — Step 2 (Modalidade) with 6 modality subforms (Finimp, Lei4131, Refinimp, Nce, BalcaoCaixa, Fgi)
- **1.6** ContratoCreatePage — Step 4 (Garantias inline) — 8 guarantee-type subforms
- **1.7** Gerar Cronograma flow (button in detail page → modal → POST → refresh cronograma tab)
- **1.8** Importar Cronograma flow (CSV-like manual table → POST)
- **1.9** Simular Antecipação flow (form → result card with savings breakdown)
- **1.10** Delete contract flow (confirm modal, policy `Gerencial`)
- **1.11** Tabela Completa export (PDF/XLSX download via dedicated endpoint)
- **1.12** Garantias CRUD (add via ContratoDetailPage modal; delete via row action)
- **1.13** Hedges CRUD nested under contract (add + list + delete + MTM view)

**Checkpoint 1**:
- [ ] User can list, filter, page, sort contracts; URL state survives reload
- [ ] User can create a Finimp contract end-to-end (all 5 steps)
- [ ] User can create one contract per modality (6 total)
- [ ] User can view detail, generate schedule, simulate, delete
- [ ] User can add CDB and SBLC garantia inside ContratoDetailPage
- [ ] User can add NDF hedge inside ContratoDetailPage; MTM displays
- [ ] All forms show backend validation errors inline
- [ ] All ≥ 80% unit-test coverage on composables and zod schemas

### Phase 2 — Painel (week 4)

- **2.1** PainelDividaPage — breakdown por moeda, MTM, alertas
- **2.2** PainelGarantiasPage — indicadores agregados, alertas
- **2.3** CalendarioVencimentosPage — grid mensal, filtros (banco/modalidade/moeda), navegação por ano
- **2.4** KpisExecutivoPage (policy Executivo) — cards de KPIs
- **2.5** EbitdaInputPage (policy Auditoria) — formulário upsert mensal

**Checkpoint 2**:
- [ ] Dashboard data loads in <1s with real Phase 7 data
- [ ] FX cotação used (SPOT vs PTAX D-1 fallback) is displayed clearly
- [ ] Calendário paginates by year; filters update URL
- [ ] Executivo policy correctly gates KPIs

### Phase 3 — Remaining CRUDs (week 5)

- **3.1** BancosListPage + BancoDetailPage (with config-antecipação card)
- **3.2** BancoFormPage (create + edit config-antecipação)
- **3.3** PlanoContasListPage + create + edit
- **3.4** ParametrosCotacaoListPage + create + edit + `/resolve` lookup helper
- **3.5** Simulador: CenarioCambialPage (3 cenários side-by-side)
- **3.6** Simulador: AntecipacaoPortfolioPage (top-5 cards + table)

**Checkpoint 3**:
- [ ] All 8 controllers have a working UI for every endpoint
- [ ] Role-gated routes return 403 page when accessed without policy
- [ ] All mutations show success/error toast

### Phase 4 — Polish, E2E, Deploy (week 6)

- **4.1** Global error boundary + Sentry-style log capture (no actual Sentry; log to console + ring buffer)
- **4.2** Loading skeletons on every list/detail page (Nordware `Skeleton`)
- **4.3** Keyboard navigation: `Cmd+K` opens `CommandPalette` (Nordware DS) with quick actions (Novo contrato, Buscar, Ir para painel...)
- **4.4** Accessibility audit: focus rings, ARIA labels, keyboard-only flows, color contrast (DS already OKLCH-tuned)
- **4.5** i18n extraction (verify no hardcoded strings remain outside `pt-BR.json`)
- **4.6** Playwright E2E for 5 golden paths: list+filter contracts; create Finimp; add CDB garantia; add NDF hedge; view painel-dívida
- **4.7** Bundle analysis + code splitting per route (route-level lazy import)
- **4.8** Build script + Dockerfile + Nginx config + production env example
- **4.9** Update root README with frontend setup steps and architecture diagram

**Checkpoint 4** (Release Candidate):
- [ ] `pnpm build` produces a working bundle <500 kB gzipped (target)
- [ ] `pnpm test` and `pnpm test:e2e` pass
- [ ] Lighthouse: Performance ≥ 90, Accessibility ≥ 95
- [ ] Manual smoke test of all 8 features against running Sgcf.Api
- [ ] Stakeholder review with screen recording of all flows

### Phase 5 (post-v1) — Real Auth

OIDC integration against `https://dev-auth.proxysgroup.com.br`. Swap mock `useAuth` for real one. No other code changes required if `useAuth` interface is stable.

---

## 9. Parallelization Opportunities

Once Phase 0 is complete, multiple features can be built in parallel by different agents/sessions:

- **Safe to parallelize** (after Phase 0.7): Bancos, PlanoContas, ParametrosCotacao, Simulador, Painel — they share infrastructure but have isolated state and routes
- **Must be sequential**: Contratos (1.1–1.13), because later steps depend on earlier ones (Garantias UI lives inside ContratoDetailPage, etc.)
- **Coordination needed**: Hedges feature has its own pages but is also embedded in ContratoDetailPage — define the `HedgesPanel.vue` component contract first, then both consumers can build in parallel

---

## 10. Risks & Mitigations

| # | Risk | Impact | Mitigation |
|---|---|---|---|
| R-01 | Server-side filter implementation lags frontend → blocks Phase 1 | High | Phase 0.1 must complete first. Define DTO contract in writing before either side starts coding. |
| R-02 | Money + Date precision drift between C# `decimal`/NodaTime and JS `number`/Date | High | Use `string` for decimals on the wire (TanStack Query passes through); never coerce to JS number until format step. Use `@js-joda/core` (mirrors NodaTime semantics). |
| R-03 | Contract creation form has 6 modality variants × stepper = combinatorial UI complexity | High | Single zod discriminated-union schema; render the right subform via `<component :is="modalityForms[modalidade]">`. Storybook stories for each modality variant. |
| R-04 | Filter state in URL + form state in component → desync bugs | Medium | URL is single source of truth; components are stateless — they read `filters.value` and emit patches via `setFilters()`. |
| R-05 | Nordware DS is v2 (OKLCH); browser color compatibility on old Safari/IE | Low | OKLCH is supported in all modern browsers (Safari 15.4+). Document minimum browser matrix. |
| R-06 | TanStack Query cache invalidation cascade (e.g., create contract should refresh painel) | Medium | Use a `cross-feature.ts` invalidation map: contract mutations invalidate `[painel]`, `[contratos]`, `[bancos, exposure]`. |
| R-07 | 27 endpoints × 6 modalities × 8 garantia types = ~120 forms; risk of inconsistency | Medium | Strict pattern enforcement: every form uses `useForm` + zod + Nordware components; PR checklist enforces. |
| R-08 | Real OIDC integration later breaks mock interface assumptions | Low | `useAuth()` interface stays stable: `{ user, isAuthenticated, hasPolicy, login, logout }`. Mock implementation matches that interface exactly. |
| R-09 | Bundle bloat from 8 features in one SPA | Medium | Route-level code splitting (`() => import('./BancosListPage.vue')`). Target initial bundle <200 kB gzipped. |
| R-10 | Backend has no idempotency-key generation strategy in spec → frontend retries cause duplicates | Medium | Generate UUID v4 client-side per submit attempt, pass as `Idempotency-Key` header; backend's `IdempotencyFilter` deduplicates. |

---

## 11. Success Criteria (v1)

- [ ] User can perform every operation supported by every endpoint (27 total)
- [ ] All filter scenarios on Contratos list work (single filter, combined filters, URL-shareable)
- [ ] Forms reject invalid input client-side AND surface server validation errors inline
- [ ] Role-gated UI: a user without policy X cannot see or trigger X actions
- [ ] All 5 Playwright golden paths pass
- [ ] Production build runs against `Sgcf.Api` deployed to staging
- [ ] Lighthouse Accessibility ≥ 95
- [ ] Stakeholder sign-off after walk-through

---

## 12. Open Questions (decide before Phase 1 starts)

1. **Idempotency-Key generation**: Confirm frontend strategy (UUID v4 per submit) matches backend `IdempotencyFilter` expectations. Verify the header name.
2. **Money input UX**: Mask with thousands separator while typing, or only format on blur? (Recommendation: format on blur to avoid caret jumps.)
3. **PDF/XLSX downloads**: Stream to disk, or open in new tab? (Recommendation: anchor download.)
4. **Save-as-draft for contracts**: Out of scope for v1? (Recommendation: yes, deferred.)
5. **Filter presets**: Per-user persistence (localStorage), per-org (backend), or none in v1? (Recommendation: none in v1.)
6. **Painel auto-refresh**: Poll every 60s? Manual refresh button only? (Recommendation: refetch on window focus + manual button; no polling.)
7. **EBITDA history view**: Read-only list of past months in addition to upsert form? Backend exposes only the upsert. (Recommendation: hide history until backend adds a query.)

---

## Approval Checklist (before kickoff)

- [ ] Architecture decisions (AD-01 through AD-12) approved
- [ ] Tech stack approved
- [ ] Repo layout (separate `sgcf-frontend/` sibling) approved
- [ ] Phase order and scope approved
- [ ] Open questions (Section 12) resolved

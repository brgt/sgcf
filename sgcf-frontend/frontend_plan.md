# SGCF Frontend — Plan & Status

## Stack

Vue 3 + TypeScript strict · Vite 5 · Pinia · TanStack Vue Query  
Design System: `@nordware/design-system` (local file dep from `../sgcf-backend/nordware-design-system`)  
Auth: mock dev token (`VITE_DEV_TOKEN`) → production JWT (Keycloak/OIDC)

---

## Already Implemented ✓

| Area | Details |
|------|---------|
| AppShell + SidebarNav | Fixed 240px sidebar, role-gated nav sections, RouterLink active states |
| TopBar | Page title from route meta, user name + role badges, logout |
| Auth store | `useAuthStore` + `useAuth`, `hasPolicy()`, mock dev bypass in `client.ts` |
| Router | All routes with lazy imports, `beforeEach` guard (auth + policy) |
| API client | `apiClient` (axios), `postIdempotent` (Idempotency-Key header), `get/post/put/del` helpers |
| URL filters | `useUrlFilters<TFilters>` — typed, syncs to URL query string, resets page on filter change |
| Painel de Dívida | KPI cards, breakdown table, MTM, alerts |
| Painel de Garantias | Distribution by type and bank |
| Calendário de Vencimentos | Monthly calendar grid |
| KPIs Executivos | Cost, term, concentration charts |
| EBITDA Input | Form with cover ratio calculation |
| Contratos — List | Server-side filters (16 params), DataTable, Pagination, `bancosMap` lookup |
| Contratos — Filter Panel | Sidebar with all filter controls, boolean encoding as `''/'true'/'false'` |
| Contratos — Detail | 4 tabs (Dados Gerais, Cronograma, Garantias, Hedges), modality detail cards |
| Contratos — Create | 3-step stepper (Dados Básicos → Detalhes → Revisão), all 6 modalities |
| Bancos — List/Detail/Form | CRUD, antecipação config edit |
| Hedges — List | Table with MTM info |
| Simulador — Cenário Cambial | FX scenario simulation with results |
| Simulador — Antecipação Portfolio | Portfolio prepayment ranking top-5 |
| Plano de Contas | List + create/edit modals |
| Parâmetros de Cotação | List + create/edit modals |
| Shared utilities | `formatMoney`, `formatLocalDate`, `formatInstant`, `parseMoney`, toast, confirm dialog |
| Tests | 53 unit tests passing (filters, dates, money, auth) |

---

## Already Fixed (Code Review Round) ✓

| Issue | Fix Applied |
|-------|-------------|
| `gerarCronograma` used `apiClient.post` (not idempotent) | → `postIdempotent` |
| `formatLocalDate(null)` crash in garantias tab | → null guard `g.dataLiberacaoPrevista ? ... : '—'` |
| Dev token injected in production builds | → gated with `import.meta.env.DEV` |
| `useBancosOptions` imported from feature layer | → moved to `src/shared/api/useBancosOptions.ts`, feature re-exports |
| `BancoFormPage` banco create not idempotent | → `postIdempotent` |
| `PlanoContasListPage` conta create not idempotent | → `postIdempotent` |
| `ParametrosListPage` parametro create not idempotent | → `postIdempotent` |
| Wrong CSS tokens: `--nw-bg-surface`, `--color-text-muted`, `--color-danger`, `--color-surface-hover` | → fixed in AppShell, TopBar, ContratoDetailPage, PainelDividaPage, BancoFormPage, PlanoContasListPage, ParametrosListPage |
| Export dropdown had no click-outside handler | → `mousedown` listener via `onMounted`/`onUnmounted` |

---

## Remaining Work

### Phase 1 — CSS Token Fixes (High · ~30 min)

Three files still use non-existent DS tokens (they have hex fallbacks so they render, but the fallback is a light-mode colour that looks wrong in the DS dark theme).

| Task | File(s) | Change |
|------|---------|--------|
| 1.1 Fix `--color-danger` | `KpisExecutivoPage.vue` | → `var(--color-error)` |
| 1.2 Fix `--color-text-muted` | `BancoDetailPage.vue`, `KpisExecutivoPage.vue`, `PainelGarantiasPage.vue` | → `var(--color-text-secondary)` |
| 1.3 Fix `--color-text` and `--color-surface-raised` | `BancoDetailPage.vue`, `AntecipacaoPortfolioPage.vue`, `CenarioCambialPage.vue` | `--color-text` → `--color-text-primary`; `--color-surface-raised` → `--color-surface-elevated` |

Verify: `grep -r "color-danger\|color-text-muted\|color-surface-raised" src/` returns nothing.

---

### Phase 2 — BancoFormPage: Native HTML → DS Components (Medium · ~1 h)

`BancoFormPage.vue` is the only page still using raw `<input>`, `<select>`, `<textarea>`, and `<input type="checkbox">` instead of DS components. All other pages already use DS form components.

| Task | What | DS Component |
|------|------|-------------|
| 2.1 Edit form — 4 boolean fields | `<input type="checkbox">` + custom label | `<Checkbox v-model label="..." />` |
| 2.2 Create form — 4 fields | `<input>` + `<select>` + raw labels | `<Input>`, `<Select>` |
| 2.3 Edit form — 7 fields | `<select>`, `<input type="number">` ×5, `<textarea>` | `<Select>`, `<Input>`, `<Textarea>` |

After migration: remove `.form-field__input`, `.form-field__label`, `.form-field__error`, `.checkbox-label`, `.checkbox-input` CSS classes.

---

### Phase 3 — ContratoDetailPage Export Dropdown (Medium · ~30 min)

The export menu is a hand-rolled div with custom click-outside logic (just added). The DS exports a `Dropdown` component that handles all of this.

| Task | Change |
|------|--------|
| 3.1 Replace custom dropdown | Import `Dropdown` from DS; use `#trigger` slot, `:items` prop, `@select` emit. Remove `showExportMenu`, `exportDropdownRef`, `handleExportClickOutside`, `onMounted`/`onUnmounted` for dropdown, `.export-dropdown__*` CSS. |

**Before implementing:** read DS `Dropdown` prop API from `node_modules/@nordware/design-system/dist/index.d.ts` to confirm exact `:items` shape and `@select` signature.

---

### Phase 4 — Theme & Cleanup (Low · ~10 min)

| Task | File | Change |
|------|------|--------|
| 4.1 Explicit dark theme | `src/app/App.vue` | Add `class="dark-theme"` to root `<div>` so theme is deterministic and not OS-preference-dependent |
| 4.2 Delete orphan style.css | `src/style.css` | Vite scaffold file, NOT imported by `main.ts`. Confirm with `grep -r "style.css" src/` then delete. |

---

## DS Token Reference (correct names)

```
--color-background        page background
--color-surface           card/panel surface
--color-surface-elevated  raised surface  (NOT --color-surface-raised)
--color-text-primary      main text       (NOT --color-text)
--color-text-secondary    muted text      (NOT --color-text-muted)
--color-text-tertiary     extra muted
--color-error             error/danger    (NOT --color-danger)
--color-success
--color-warning
--color-info
--color-primary           brand accent
--color-hover             hover overlay
--color-border
--color-border-hover
```

---

## DS Component Inventory (what is exported)

**Atoms:** Avatar · Badge · Button · Checkbox · HealthCircle · Input · Progress · Radio · Spinner · Textarea  
**Molecules:** Alert · Breadcrumb · Card · DataTable · Dropdown · EmptyState · FlywheelPhase · Pagination · Select · Skeleton · Tabs · Tooltip  
**Organisms:** Modal  
**Templates:** DashboardGrid · PageHeader · PageLayout  
**Extras:** AnomalyPanel · CommandPalette · OrderCard · Toast · ToastContainer

All components currently imported by the frontend are correctly named and exist in the DS. No missing imports.

---

## Definition of Done

- [ ] `npm test` — all tests pass
- [ ] `npm run build` — exits 0, zero TS errors
- [ ] `grep -r "color-danger\|color-text-muted\|color-surface-raised" src/` → no results
- [ ] No `<input type="checkbox">` or bare `<select>` in `BancoFormPage.vue`
- [ ] No custom click-outside logic for the export dropdown in `ContratoDetailPage.vue`
- [ ] Root element has `class="dark-theme"`
- [ ] `src/style.css` deleted

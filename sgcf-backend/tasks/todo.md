# SGCF Frontend — Task Checklist

> Companion to `tasks/plan.md`. Tasks are vertical slices ordered by dependency. Mark `[x]` when verification passes.
>
> Sizing: **XS** = 1 file · **S** = 1–2 files · **M** = 3–5 files · **L** = 5–8 files

---

## Phase 0 — Foundation

### Backend extensions (must finish before frontend list pages)

- [ ] **0.1.1 [M]** Extend `ListContratosQuery` with filters + paging + sort
  - **Acceptance:**
    - `ListContratosQuery` accepts: `Search, BancoId, Modalidade, Moeda, Status, DataVencimentoDe, DataVencimentoAte, ValorPrincipalMin, ValorPrincipalMax, TemHedge, TemGarantia, TemAlertaVencimento, Page, PageSize, Sort, Dir`
    - Returns `PagedResult<ContratoDto>(Items, Total, Page, PageSize)`
    - `ValorPrincipalMin/Max` compare BRL-equivalent values using PTAX D-1 (consistent with painel)
    - `Search` uses ILIKE on `NumeroExterno` and `CodigoInterno`
    - Sort whitelist: `DataVencimento, DataContratacao, ValorPrincipal, NumeroExterno`
  - **Verify:** `dotnet test --filter "FullyQualifiedName~ListContratos"` covers each filter dimension + pagination boundaries
  - **Files:** `ListContratosQuery.cs`, `ContratosController.cs`, `IContratoRepository.cs` + EF impl, new `PagedResult<T>.cs`

- [ ] **0.1.2 [S]** Extend `ListBancosQuery` with `Search` filter (codigoCompe / razaoSocial / apelido)
  - **Acceptance:** `?search=brad` returns matching banks; paging optional (banks fit on one page typically)
  - **Verify:** integration test against `/api/v1/bancos?search=...`
  - **Files:** `ListBancosQuery.cs`, `BancosController.cs`

- [ ] **0.1.3 [S]** Extend `ListPlanoContasQuery` with `Search` + `Ativo` filter
  - **Acceptance:** `?search=...&ativo=true` works; `Ativo` defaults to all
  - **Verify:** integration test
  - **Files:** `ListPlanoContasQuery.cs`, `PlanoContasController.cs`

- [ ] **0.1.4 [S]** Configure CORS on `Sgcf.Api` for `http://localhost:5173` (Vite dev)
  - **Acceptance:** Preflight + actual requests succeed from frontend dev server
  - **Verify:** `curl -H "Origin: http://localhost:5173" -X OPTIONS http://localhost:5000/api/v1/bancos -i` returns 204 with `Access-Control-Allow-Origin`
  - **Files:** `Program.cs`

- [ ] **0.1.5 [XS]** Document the `Idempotency-Key` header contract in API docs
  - **Acceptance:** Markdown note in `docs/architecture/` describing format (UUID v4) and which endpoints honor it
  - **Verify:** doc exists; lists all 3 idempotent endpoints
  - **Files:** `docs/architecture/idempotency.md`

### Frontend scaffolding

- [ ] **0.2.1 [L]** Initialize `sgcf-frontend/` Vite + Vue 3 + TS strict
  - **Acceptance:**
    - `pnpm dev` boots and serves `App.vue` at `localhost:5173`
    - `tsconfig.json` has `strict: true, noUncheckedIndexedAccess: true, exactOptionalPropertyTypes: true`
    - ESLint + Prettier configured; `pnpm lint` passes on empty project
    - `pnpm test` (Vitest) runs successfully on a trivial test
  - **Verify:** `pnpm dev && pnpm build && pnpm test && pnpm lint`
  - **Files:** project scaffolding (~10 files)

- [ ] **0.2.2 [S]** Wire Nordware DS as `file:` dependency in `package.json`
  - **Acceptance:** `import { Button, DataTable } from '@nordware/design-system'` resolves; `import '@nordware/design-system/styles'` loads CSS
  - **Verify:** smoke `App.vue` renders a `Button` and `DataTable` correctly
  - **Files:** `package.json`, `App.vue`

- [ ] **0.2.3 [S]** Configure UnoCSS to match Nordware DS preset (Carbon + MDI icons)
  - **Acceptance:** `i-carbon-home` and `i-mdi-cart` classes render icons
  - **Verify:** smoke page shows icons
  - **Files:** `uno.config.ts`, `vite.config.ts`

- [ ] **0.2.4 [S]** Vite proxy `/api` → `http://localhost:5000`
  - **Acceptance:** `fetch('/api/v1/bancos')` from dev server hits backend with no CORS issue
  - **Verify:** smoke fetch returns 401 (auth gate) — proves proxy works
  - **Files:** `vite.config.ts`

- [ ] **0.3.1 [M]** Mock auth composable + `RoleGate` component
  - **Acceptance:**
    - `useAuth()` returns `{ user, isAuthenticated, hasPolicy(name), login(), logout() }`
    - Reads `VITE_DEV_TOKEN` and `VITE_DEV_ROLES` from `import.meta.env`
    - `hasPolicy('Escrita')` returns true when role is `tesouraria` or `admin` (mirrors backend Policies.cs)
    - `<RoleGate policy="Admin"><AdminButton/></RoleGate>` hides children when policy fails
  - **Verify:** unit test all 6 policies with various role sets
  - **Files:** `shared/auth/useAuth.ts`, `shared/auth/RoleGate.vue`, `shared/auth/policies.ts`

- [ ] **0.4.1 [M]** Axios client with interceptors
  - **Acceptance:**
    - Adds `Authorization: Bearer <token>` from `useAuth().token`
    - Adds `Idempotency-Key` header (UUID v4) on POST when explicitly enabled via per-request flag
    - On 401: clear auth + redirect `/login`
    - On 403: toast error + reject promise (no redirect; user stays on page)
    - On network error: typed error message for UI
  - **Verify:** unit tests with mocked Axios for each interceptor branch
  - **Files:** `shared/api/client.ts`, `shared/api/types.ts` (Error envelope shape)

- [ ] **0.4.2 [L]** TypeScript DTOs mirroring all C# DTOs
  - **Acceptance:** Every DTO from API map has a corresponding TS interface in `shared/api/types.ts`. Enums (`ModalidadeContrato`, `Moeda`, `StatusContrato`, `TipoCotacao`, `TipoGarantia`, etc.) defined as const objects + type unions.
  - **Verify:** type-check passes; no `any` in the file
  - **Files:** `shared/api/types.ts`, `shared/api/enums.ts`

- [ ] **0.5.1 [M]** `useUrlFilters` + `usePagedList` composables
  - **Acceptance:**
    - `useUrlFilters(schema, defaults)` returns `{ filters, setFilters, resetFilters }`; filters reactive to URL changes; defaults are stripped from URL
    - `usePagedList({ queryKey, fetchFn, filters, debounceMs })` wraps TanStack Query with `keepPreviousData` and debounced filter changes
  - **Verify:** Vitest specs covering: parse from URL, patch URL, reset, debounce timing
  - **Files:** `shared/filters/useUrlFilters.ts`, `shared/filters/usePagedList.ts`

- [ ] **0.5.2 [S]** Money formatter + parser
  - **Acceptance:**
    - `formatMoney({ valor: '1234.567', moeda: 'BRL' })` → `'R$ 1.234,57'`
    - `formatMoney({ valor: '1234.567', moeda: 'USD' })` → `'US$ 1,234.57'`
    - `parseMoney('1.234,56', 'BRL')` → `'1234.56'` (string, never number)
    - Never coerces decimal to JS number
  - **Verify:** Vitest specs for BRL, USD, EUR, JPY, CNY
  - **Files:** `shared/money/formatMoney.ts`, `shared/money/parseMoney.ts`, `shared/money/Money.ts`

- [ ] **0.5.3 [S]** Date formatters using `@js-joda/core`
  - **Acceptance:** `formatLocalDate('2026-05-12')` → `'12/05/2026'`; `formatInstant('2026-05-12T15:30:00Z')` → `'12/05/2026 12:30'` (America/Sao_Paulo)
  - **Verify:** Vitest specs, including DST boundary
  - **Files:** `shared/dates/formatDate.ts`, `shared/dates/formatInstant.ts`

- [ ] **0.5.4 [S]** `ConfirmDialog`, `ErrorBoundary`, `LoadingState`
  - **Acceptance:** `useConfirm({ title, message, variant: 'danger' })` returns promise<boolean>; ErrorBoundary catches render errors and shows fallback
  - **Verify:** Vitest specs + Storybook stories
  - **Files:** `shared/ui/*.vue`

- [ ] **0.5.5 [S]** Toast wrapper using Nordware `ToastContainer`
  - **Acceptance:** `toast.success(msg)`, `toast.error(msg)`, `toast.info(msg)` work app-wide
  - **Verify:** Vitest spec; smoke page demo
  - **Files:** `shared/ui/toast.ts`

- [ ] **0.6.1 [M]** AppShell layout + SidebarNav + topbar
  - **Acceptance:**
    - Sidebar shows menu sections (Painel, Contratos, Bancos, Hedges, Simulador, Configurações)
    - Each menu item respects `RoleGate`
    - Topbar shows user name + role + logout button
    - Main slot renders `<router-view>`
  - **Verify:** screenshot test on `/` route
  - **Files:** `layouts/AppShell.vue`, `layouts/SidebarNav.vue`, `app/router.ts`

- [ ] **0.7.1 [S]** vue-i18n setup with `pt-BR.json` seed
  - **Acceptance:** `t('common.actions.save')` returns `'Salvar'`; missing keys log a warning
  - **Verify:** Vitest spec on missing-key fallback
  - **Files:** `app/providers/i18n.ts`, `shared/i18n/locales/pt-BR.json`

- [ ] **0.7.2 [S]** Global Vue Query setup with sensible defaults
  - **Acceptance:** `staleTime: 30_000`, `refetchOnWindowFocus: true`, retry strategy on idempotent GETs only
  - **Verify:** unit test on retry behavior
  - **Files:** `app/providers/query.ts`

### **Checkpoint 0**
- [ ] Backend filter endpoints return correct shape
- [ ] Frontend boots and shows AppShell
- [ ] All shared composables have ≥80% unit coverage
- [ ] Lint + type-check + tests green

---

## Phase 1 — Contratos (★)

- [ ] **1.1 [L]** ContratosListPage with filter bar + DataTable + pagination
  - **Acceptance:** Filters: search, bancoId, modalidade, moeda, status, vencDe/Ate, valorMin/Max. Active filter chips render. Empty state shows current filters. URL is source of truth.
  - **Verify:** Vitest spec on each filter; Playwright smoke test
  - **Files:** `features/contratos/pages/ContratosListPage.vue`, `ContratosFiltersBar.vue`, `useContratosQueries.ts`

- [ ] **1.2 [M]** Advanced filters (temHedge / temGarantia / temAlerta) — collapsible section
  - **Acceptance:** Boolean filters in expandable section; reflected in URL and chips
  - **Verify:** Vitest spec
  - **Files:** `ContratosFiltersBar.vue`

- [ ] **1.3 [L]** ContratoDetailPage with tabs (Resumo / Cronograma / Garantias / Hedges / Simulação)
  - **Acceptance:** Tabs render contract summary, cronograma table, garantias panel, hedges panel, simulação form. URL tracks active tab `?tab=cronograma`.
  - **Verify:** Playwright smoke test
  - **Files:** `ContratoDetailPage.vue`, `ContratoResumoTab.vue`, `CronogramaTable.vue`, `GarantiasPanel.vue`, `HedgesPanel.vue`

- [ ] **1.4 [L]** ContratoCreatePage stepper — Steps 1, 3, 5 (Dados básicos, Taxa & Prazo, Revisão)
  - **Acceptance:** Form is split into 5 steps; Step 1, 3, 5 implemented; navigation forward/back works; final POST creates contract with Idempotency-Key
  - **Verify:** Playwright test creates a Finimp contract (uses Step 2 from 1.5)
  - **Files:** `ContratoCreatePage.vue`, `Step1_DadosBasicos.vue`, `Step3_TaxaPrazo.vue`, `Step5_Revisao.vue`

- [ ] **1.5 [L]** Step 2 — Modalidade selector + 6 modality subforms
  - **Acceptance:** Selecting modalidade reveals correct subform (Finimp, Lei4131, Refinimp, Nce, BalcaoCaixa, Fgi). Each subform has its own zod schema. Refinimp asks for `contratoPaiId` via a contract picker.
  - **Verify:** Vitest specs per modality schema; Playwright creates each of the 6 modalities
  - **Files:** `Step2_Modalidade.vue`, `modalityForms/FinimpForm.vue`, `Lei4131Form.vue`, `RefinimpForm.vue`, `NceForm.vue`, `BalcaoCaixaForm.vue`, `FgiForm.vue`, `schemas/*.ts`

- [ ] **1.6 [L]** Step 4 — Inline Garantias add (8 types)
  - **Acceptance:** User can add 0..N garantias inline before final POST. Each garantia has type-specific subform (CDB, SBLC, Aval, Alienação, Duplicatas, Recebíveis, Boleto, FGI).
  - **Verify:** Vitest per schema; Playwright adds CDB + SBLC during creation
  - **Files:** `Step4_Garantias.vue`, `garantias/forms/*.vue`

- [ ] **1.7 [M]** Gerar Cronograma flow
  - **Acceptance:** Button on detail page → modal with `GerarCronogramaRequest` form → POST → cronograma tab refreshes via cache invalidation
  - **Verify:** Vitest mutation hook test; Playwright smoke
  - **Files:** `GerarCronogramaModal.vue`, mutation in `useContratosQueries.ts`

- [ ] **1.8 [M]** Importar Cronograma flow (manual parcelas)
  - **Acceptance:** Modal with editable parcelas grid (numero, dataVenc, valorPrincipal, valorJuros, moeda) + POST `/importar-cronograma`
  - **Verify:** Vitest; Playwright imports 3 parcelas
  - **Files:** `ImportarCronogramaModal.vue`

- [ ] **1.9 [M]** Simular Antecipação flow
  - **Acceptance:** Form (dataReferencia, modo: total/parcial, valorParcial?) → POST → result card shows savings breakdown + 3 cenários
  - **Verify:** Playwright smoke
  - **Files:** `ContratoSimularPage.vue`, `SimulacaoResultadoCard.vue`

- [ ] **1.10 [S]** Delete contract flow
  - **Acceptance:** Row action + detail-page button → ConfirmDialog → DELETE → redirect to list + toast. Hidden unless policy `Gerencial`.
  - **Verify:** Playwright smoke
  - **Files:** `ContratosListPage.vue`, `ContratoDetailPage.vue`

- [ ] **1.11 [M]** Tabela Completa export (PDF/XLSX)
  - **Acceptance:** Buttons "Exportar PDF" and "Exportar XLSX" trigger download. Optional `dataReferencia` + override `cotacao` inputs.
  - **Verify:** Playwright clicks download, asserts file received
  - **Files:** `TabelaCompletaExportButton.vue`

- [ ] **1.12 [L]** Garantias add/delete inside ContratoDetailPage
  - **Acceptance:** "Adicionar garantia" opens modal with type selector + subform (8 types reused from Step 4). Delete via row action.
  - **Verify:** Playwright adds CDB then deletes
  - **Files:** `GarantiasPanel.vue`, `AddGarantiaModal.vue`

- [ ] **1.13 [L]** Hedges add/delete inside ContratoDetailPage + MTM view
  - **Acceptance:** "Novo hedge" modal (Tipo, Contraparte, Notional, Moeda, Datas, Strikes). MTM card shows current MtmAReceberBrl/MtmAPagarBrl/MtmLiquidoBrl.
  - **Verify:** Playwright adds NDF, checks MTM appears
  - **Files:** `HedgesPanel.vue`, `AddHedgeModal.vue`, `MtmCard.vue`

### **Checkpoint 1**
- [ ] Filter chips + URL persistence verified
- [ ] One contract per modality created end-to-end
- [ ] Garantia + Hedge add/delete works
- [ ] All forms validate client + server side
- [ ] Composable test coverage ≥ 80%

---

## Phase 2 — Painel

- [ ] **2.1 [L]** PainelDividaPage (default landing)
  - **Acceptance:** Breakdown por moeda (table), MTM card, Alertas list, "Última atualização" timestamp, "Atualizar" button
  - **Verify:** Playwright loads against running Sgcf.Api with seed data
  - **Files:** `PainelDividaPage.vue`, `DividaPorMoedaCard.vue`, `AjusteMtmCard.vue`

- [ ] **2.2 [M]** PainelGarantiasPage
  - **Acceptance:** Indicadores (cobertura total, cobertura líquida, % faturamento cartão comprometido) + alertas
  - **Verify:** Playwright smoke
  - **Files:** `PainelGarantiasPage.vue`

- [ ] **2.3 [L]** CalendarioVencimentosPage
  - **Acceptance:** Year-month grid (12 months); per-cell totals in BRL; filters (banco, modalidade, moeda); year navigation
  - **Verify:** Playwright; check URL `?ano=2026&bancoId=...`
  - **Files:** `CalendarioVencimentosPage.vue`, `CalendarioMesGrid.vue`

- [ ] **2.4 [M]** KpisExecutivoPage (policy Executivo)
  - **Acceptance:** KPI cards: dívida total, custo médio, prazo médio, concentração por banco
  - **Verify:** Playwright with mock role=diretor
  - **Files:** `KpisExecutivoPage.vue`

- [ ] **2.5 [M]** EbitdaInputPage (policy Auditoria)
  - **Acceptance:** Form (Ano, Mes, ValorBrl) → POST `/painel/ebitda` → toast success
  - **Verify:** Vitest mutation; Playwright smoke
  - **Files:** `EbitdaInputForm.vue`

### **Checkpoint 2**
- [ ] All 4 dashboards load <1s
- [ ] Cotação type (SPOT/PTAX D-1) clearly displayed
- [ ] Policies enforced

---

## Phase 3 — Remaining CRUDs

- [ ] **3.1 [M]** BancosListPage + BancoDetailPage
  - **Acceptance:** List with search filter; Detail shows config-antecipação card
  - **Verify:** Playwright smoke
  - **Files:** `BancosListPage.vue`, `BancoDetailPage.vue`

- [ ] **3.2 [M]** BancoFormPage (create + edit config-antecipação)
  - **Acceptance:** Create via POST `/bancos` (policy Admin); edit config via PUT `/bancos/{id}/config-antecipacao` (policy Admin)
  - **Verify:** Playwright creates a bank
  - **Files:** `BancoFormPage.vue`

- [ ] **3.3 [M]** PlanoContasListPage + create + edit
  - **Acceptance:** List with `ativo` + search filters; create/edit via modal or page
  - **Verify:** Playwright creates a conta
  - **Files:** `PlanoContasListPage.vue`, `PlanoContasFormPage.vue`

- [ ] **3.4 [M]** ParametrosCotacaoListPage + create + edit + `/resolve` helper
  - **Acceptance:** List of params; create modal; edit modal; "Resolve" helper button to test `?bancoId=...&modalidade=...`
  - **Verify:** Playwright tests resolve flow
  - **Files:** `ParametrosListPage.vue`, `ParametroFormPage.vue`, `ResolveTipoCotacaoTester.vue`

- [ ] **3.5 [M]** Simulador: CenarioCambialPage
  - **Acceptance:** Form (deltas USD/EUR/JPY/CNY) → POST → 3 cenários side-by-side (pessimista, realista, otimista)
  - **Verify:** Playwright smoke
  - **Files:** `CenarioCambialPage.vue`, `CenarioCard.vue`

- [ ] **3.6 [M]** Simulador: AntecipacaoPortfolioPage
  - **Acceptance:** Form (caixaDisponivelBrl, taxaCdiAa?) → POST → top-5 contracts ranked by net savings
  - **Verify:** Playwright smoke
  - **Files:** `AntecipacaoPortfolioPage.vue`

### **Checkpoint 3**
- [ ] All 8 controllers have working UI for every endpoint
- [ ] Role-gated routes return 403 page
- [ ] All mutations show success/error toast

---

## Phase 4 — Polish, E2E, Deploy

- [ ] **4.1 [S]** Global error boundary + console ring buffer
  - **Acceptance:** Render errors caught; "Algo deu errado" fallback with reload button; last 100 errors kept in ring buffer for support copy
  - **Verify:** Vitest forced error test
  - **Files:** `shared/ui/ErrorBoundary.vue`

- [ ] **4.2 [M]** Skeleton loaders on all list/detail pages
  - **Acceptance:** Every page with async data uses Nordware `Skeleton` during initial load
  - **Verify:** Visual review per page
  - **Files:** various pages

- [ ] **4.3 [M]** `Cmd+K` CommandPalette integration
  - **Acceptance:** Global shortcut opens palette with quick actions (Novo contrato, Buscar contratos, Painel dívida, Simulador cambial). Closes on ESC. Keyboard navigable.
  - **Verify:** Playwright keyboard test
  - **Files:** `layouts/AppShell.vue` + palette config

- [ ] **4.4 [M]** Accessibility audit
  - **Acceptance:** axe-core no violations on 5 main pages. Focus rings visible. ARIA labels on icon-only buttons. Keyboard-only navigation works.
  - **Verify:** `@axe-core/playwright` integration test
  - **Files:** various

- [ ] **4.5 [S]** i18n extraction audit
  - **Acceptance:** No hardcoded user-visible PT-BR strings outside `pt-BR.json`. ESLint custom rule or grep-based CI check.
  - **Verify:** Custom script `pnpm i18n:audit`
  - **Files:** `scripts/i18n-audit.ts`

- [ ] **4.6 [L]** Playwright E2E suite — 5 golden paths
  - **Acceptance:**
    1. List + filter contracts (paste URL, refresh, navigate)
    2. Create Finimp contract end-to-end
    3. Add CDB garantia
    4. Add NDF hedge + check MTM
    5. Load painel-dívida + verify breakdown
  - **Verify:** `pnpm test:e2e` passes in CI
  - **Files:** `tests/e2e/*.spec.ts`

- [ ] **4.7 [M]** Route-level code splitting + bundle analysis
  - **Acceptance:** Every page uses `() => import('...')`; initial route bundle <200 kB gzipped; total <500 kB gzipped
  - **Verify:** `pnpm build && vite-bundle-visualizer`
  - **Files:** `app/router.ts`

- [ ] **4.8 [M]** Production build artifacts (Dockerfile + Nginx config)
  - **Acceptance:** `docker build .` produces image serving the SPA with proper SPA fallback (`try_files $uri /index.html`) and gzip
  - **Verify:** Build and curl static assets
  - **Files:** `Dockerfile`, `nginx.conf`, `.env.production.example`

- [ ] **4.9 [S]** Update root `README.md` with frontend setup steps
  - **Acceptance:** Section "Frontend (sgcf-frontend)" with quickstart, env vars, dev/build commands
  - **Verify:** Cold-start a teammate from the README
  - **Files:** `sgcf-frontend/README.md`, optionally root `README.md`

### **Checkpoint 4 — Release Candidate**
- [ ] `pnpm build` succeeds; bundle within budget
- [ ] `pnpm test` and `pnpm test:e2e` green
- [ ] Lighthouse Performance ≥ 90, Accessibility ≥ 95
- [ ] Manual smoke test against running Sgcf.Api
- [ ] Stakeholder walkthrough recorded

---

## Phase 5 — Real Auth (post-v1)

- [ ] **5.1 [M]** OIDC integration with `oidc-client-ts`
- [ ] **5.2 [S]** Swap mock `useAuth` for real implementation (interface unchanged)
- [ ] **5.3 [S]** Add logout flow (revoke token, redirect)
- [ ] **5.4 [M]** Silent renew + session expiry handling

---

## Cross-cutting verification (run before each checkpoint)

```bash
# in sgcf-frontend/
pnpm lint                  # ESLint + Prettier
pnpm type-check            # vue-tsc --noEmit
pnpm test                  # Vitest unit
pnpm test:e2e              # Playwright (only after Phase 4.6)
pnpm build                 # production build
```

```bash
# in sgcf-backend/
dotnet test sgcf-backend.sln --filter "Category!=Slow"
dotnet ef database update --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api
dotnet run --project src/Sgcf.Api
```

---

## Open decisions (block Phase 1 start)

See `tasks/plan.md` section 12. Need answers on:
1. Idempotency-Key header name confirmation
2. Money input mask vs. format-on-blur
3. PDF/XLSX download UX
4. Save-as-draft (in/out of scope)
5. Filter presets (in/out of scope)
6. Painel auto-refresh strategy
7. EBITDA history view (block until backend exposes it)

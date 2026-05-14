# SGCF Frontend — Design System Alignment Tasks

> Source of truth: `tasks/plan.md`
> DS reference: `storybook-static/` + `node_modules/@nordware/design-system/dist/index.d.ts`

---

## Phase 1 — CSS Token Fixes (High Priority)

- [ ] **TASK 1.1** — Fix `--color-danger` → `--color-error`
  - File: `src/features/painel/pages/KpisExecutivoPage.vue`
  - Change: `var(--color-danger, ...)` → `var(--color-error)`
  - Verify: `grep -r "color-danger" src/` returns nothing

- [ ] **TASK 1.2** — Fix `--color-text-muted` → `--color-text-secondary`
  - Files: `BancoDetailPage.vue`, `KpisExecutivoPage.vue`, `PainelGarantiasPage.vue`
  - Change: `var(--color-text-muted, #6b7280)` → `var(--color-text-secondary)`
  - Verify: `grep -r "color-text-muted" src/` returns nothing

- [ ] **TASK 1.3** — Fix `--color-text` and `--color-surface-raised`
  - Files: `BancoDetailPage.vue`, `AntecipacaoPortfolioPage.vue`, `CenarioCambialPage.vue`
  - Changes:
    - `var(--color-text, #111827)` → `var(--color-text-primary)`
    - `var(--color-surface-raised, ...)` → `var(--color-surface-elevated)`
  - Verify: `grep -r "color-surface-raised" src/` returns nothing

---

## Phase 2 — BancoFormPage: Native → DS Components (Medium Priority)

- [ ] **TASK 2.1** — Replace native checkboxes with DS `Checkbox` in edit form
  - File: `src/features/bancos/pages/BancoFormPage.vue`
  - Fields: `aceitaLiquidacaoTotal`, `aceitaLiquidacaoParcial`, `exigeAnuenciaExpressa`, `exigeParcelaInteira`
  - Import `Checkbox` from `@nordware/design-system`
  - Remove: `.checkbox-label`, `.checkbox-input` CSS + `.form-field--checkbox` grid config

- [ ] **TASK 2.2** — Replace native inputs/select in create form with DS `Input`/`Select`
  - File: `src/features/bancos/pages/BancoFormPage.vue`
  - Fields: `codigoCompe`, `razaoSocial`, `apelido`, `padraoAntecipacao`
  - Import `Input`, `Select` from `@nordware/design-system` (if not already)
  - Pass validation errors via `:error` prop
  - Remove native `.form-field__input` CSS (if no longer needed)

- [ ] **TASK 2.3** — Replace native fields in edit form with DS `Select`/`Input`/`Textarea`
  - File: `src/features/bancos/pages/BancoFormPage.vue`
  - Fields: `padraoAntecipacao` (Select), numeric fields (Input), `observacoesAntecipacao` (Textarea)
  - Import `Textarea` from `@nordware/design-system`
  - Remove all `.form-field__*` CSS after migration complete

---

## Phase 3 — ContratoDetailPage Dropdown (Medium Priority)

- [ ] **TASK 3.1** — Replace custom export dropdown with DS `Dropdown`
  - File: `src/features/contratos/pages/ContratoDetailPage.vue`
  - Import `Dropdown` from `@nordware/design-system`
  - Use `#trigger` slot + `:items` prop + `@select` emit
  - Remove: `showExportMenu`, `exportDropdownRef`, `handleExportClickOutside`, `onMounted`/`onUnmounted` for dropdown, `.export-dropdown__*` CSS
  - **Before implementing:** read DS `Dropdown` prop types from `node_modules/@nordware/design-system/dist/index.d.ts`

---

## Phase 4 — Theme & Cleanup (Low Priority)

- [ ] **TASK 4.1** — Apply explicit `dark-theme` class to root element
  - File: `src/app/App.vue`
  - Change: add `class="dark-theme"` to root `<div>`

- [ ] **TASK 5.1** — Remove orphan `src/style.css`
  - Run: `grep -r "style.css" src/` to confirm it is not imported anywhere
  - If confirmed: delete `src/style.css`

---

## Definition of Done

All tasks complete when:
- [ ] `npm test` — all 53 tests pass
- [ ] `npm run build` — exits 0, no TS errors
- [ ] `grep -r "color-danger\|color-text-muted\|color-surface-raised" src/` returns no results
- [ ] `grep -r "\"--color-text\"" src/` returns no results (only `--color-text-primary`, `--color-text-secondary`, etc.)
- [ ] No `<input type="checkbox">` outside of native-date contexts in `BancoFormPage.vue`
- [ ] No custom dropdown click-outside logic in `ContratoDetailPage.vue`

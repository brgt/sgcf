# Implementation Plan: Nordware Design System v2.0 Alignment

## Overview

The SGCF frontend imports `@nordware/design-system` (local file dependency from `../sgcf-backend/nordware-design-system`). The Storybook static export in `storybook-static/` is the authoritative reference for the DS API.

After a full gap analysis, the **package install, all component imports, all component prop names, and the CSS styles import are already correct**. There are no missing packages and no wrong import paths.

What remains is a set of targeted fixes in three areas:
1. Wrong CSS custom property names (four tokens)
2. Native HTML form elements in `BancoFormPage.vue` instead of DS components
3. Custom dropdown in `ContratoDetailPage.vue` instead of DS `Dropdown`
4. Minor: explicit theme class, orphan style.css cleanup

---

## Architecture Decisions

- **Package:** `@nordware/design-system: file:../sgcf-backend/nordware-design-system` — already installed, dist present in `node_modules`. No reinstall needed.
- **CSS entry:** `main.ts` imports `@nordware/design-system/styles` → maps to `dist/style.css` via package.json exports. Correct.
- **Dark theme:** The DS defaults to dark theme on `:root`. The frontend does not need to set a class to get dark mode. However, adding `class="dark-theme"` to `<body>` or root element makes the theme explicit and prevents OS-preference flipping.
- **Native date inputs:** DS `Input` does not support `type="date"`. Existing pattern of native `<input type="date">` with DS tokens for styling is correct and intentional — keep it.
- **Toast:** Custom toast queue (`shared/ui/toast.ts`) rendering individual DS `Toast` atoms is correct. DS `ToastContainer` is optional and not required.

---

## Gap Analysis Summary

### What is already correct

| Area | Status |
|------|--------|
| Package install | ✓ Installed correctly |
| CSS styles import | ✓ `@nordware/design-system/styles` in `main.ts` |
| All component named imports | ✓ Match DS exports exactly |
| DataTable `Column<T>` interface | ✓ Locally defined, matches DS interface |
| Pagination props/events | ✓ `currentPage`, `totalPages`, `@update:currentPage` |
| Tabs API | ✓ `items`, `v-model`, `TabItem.key/label/icon` |
| Modal API | ✓ `v-model`, `title`, `size`, `#footer` slot |
| Select API | ✓ `options`, `v-model`, `label`, `error`, `disabled` |
| Input API | ✓ `v-model`, `type`, `label`, `error`, `full-width` |
| Badge variants | ✓ `default/primary/success/warning/danger/info` |
| Alert variants | ✓ `error/warning/info/success` (NOT `danger`) |
| PageLayout props | ✓ `max-width`, `has-sidebar`, `sidebar-width` |
| DashboardGrid props | ✓ `columns`, `gap`, `responsive` |

### What needs fixing

| Area | Files affected | Priority |
|------|----------------|----------|
| `--color-danger` → `--color-error` | KpisExecutivoPage.vue | High |
| `--color-text-muted` → `--color-text-secondary` | BancoDetailPage.vue, KpisExecutivoPage.vue, PainelGarantiasPage.vue | High |
| `--color-text` → `--color-text-primary` | BancoDetailPage.vue | High |
| `--color-surface-raised` → `--color-surface-elevated` | AntecipacaoPortfolioPage.vue, CenarioCambialPage.vue | High |
| Native checkboxes → DS `Checkbox` | BancoFormPage.vue | Medium |
| Native inputs/select/textarea → DS components | BancoFormPage.vue | Medium |
| Custom export dropdown → DS `Dropdown` | ContratoDetailPage.vue | Medium |
| Explicit `dark-theme` class on root | App.vue or index.html | Low |
| Remove orphan `src/style.css` | style.css | Low |

---

## DS Tokens Reference (from `global.css`)

The following tokens are defined and available:

```css
/* Backgrounds */
--color-background       /* page background */
--color-surface          /* card/panel surface */
--color-surface-elevated /* raised surface (was --color-surface-raised — does NOT exist) */

/* Text */
--color-text-primary     /* main text (was --color-text — does NOT exist) */
--color-text-secondary   /* secondary/muted text (was --color-text-muted — does NOT exist) */
--color-text-tertiary    /* extra-muted text */

/* Status */
--color-error            /* error state (was --color-danger — does NOT exist) */
--color-success          /* success state */
--color-warning          /* warning state */
--color-info             /* info state */

/* Interactive */
--color-primary          /* brand/accent */
--color-hover            /* hover state overlay */
--color-border           /* default border */
--color-border-hover     /* focused/hovered border */
```

---

## Phase 1 — CSS Token Fixes

### Task 1.1 — Fix `--color-danger` → `--color-error`

**Files:** `src/features/painel/pages/KpisExecutivoPage.vue`

**Changes:**
- Replace all occurrences of `var(--color-danger, ...)` with `var(--color-error)`

**Acceptance criteria:**
- `grep -r "color-danger" src/` returns no results
- KPI page renders error state in DS theme color (not the fallback `#dc2626`)

**Size:** S — 1 file

---

### Task 1.2 — Fix `--color-text-muted` → `--color-text-secondary`

**Files:**
- `src/features/bancos/pages/BancoDetailPage.vue`
- `src/features/painel/pages/KpisExecutivoPage.vue`
- `src/features/painel/pages/PainelGarantiasPage.vue`

**Changes:**
- Replace all `var(--color-text-muted, #6b7280)` with `var(--color-text-secondary)`

**Acceptance criteria:**
- `grep -r "color-text-muted" src/` returns no results
- Secondary text renders using DS dark-theme secondary color

**Size:** S — 3 files, ~5 occurrences

---

### Task 1.3 — Fix `--color-text` and `--color-surface-raised`

**Files:**
- `src/features/bancos/pages/BancoDetailPage.vue`
- `src/features/simulador/pages/AntecipacaoPortfolioPage.vue`
- `src/features/simulador/pages/CenarioCambialPage.vue`

**Changes:**
- `var(--color-text, #111827)` → `var(--color-text-primary)`
- `var(--color-surface-raised, var(--color-surface))` → `var(--color-surface-elevated)`

**Acceptance criteria:**
- `grep -r "color-surface-raised\|\"--color-text\"" src/` returns no results

**Size:** S — 3 files

---

## Phase 2 — BancoFormPage Native → DS Components

### Task 2.1 — Replace native checkboxes with DS `Checkbox` in edit form

**File:** `src/features/bancos/pages/BancoFormPage.vue`

**Context:** The edit form uses raw `<input type="checkbox">` + custom `<label>` wrappers for `aceitaLiquidacaoTotal`, `aceitaLiquidacaoParcial`, `exigeAnuenciaExpressa`, `exigeParcelaInteira`. The DS exports a `Checkbox` component with `v-model` and `label` prop.

**Changes:**
```html
<!-- BEFORE -->
<div class="form-field form-field--checkbox">
  <label class="checkbox-label">
    <input v-model="editForm.aceitaLiquidacaoTotal" type="checkbox" class="checkbox-input" />
    <span>Aceita Liquidação Total</span>
  </label>
</div>

<!-- AFTER -->
<div class="form-field">
  <Checkbox v-model="editForm.aceitaLiquidacaoTotal" label="Aceita Liquidação Total" />
</div>
```

Apply same pattern for the other 3 boolean fields. Add `Checkbox` to the DS import in the `<script setup>`.

**Acceptance criteria:**
- The 4 boolean fields in edit mode use `<Checkbox>` from `@nordware/design-system`
- No `<input type="checkbox">` remains in `BancoFormPage.vue`
- Remove `.checkbox-label`, `.checkbox-input` CSS classes (no longer needed)

**Size:** S — 1 file, 4 fields

---

### Task 2.2 — Replace native inputs/select in create form with DS components

**File:** `src/features/bancos/pages/BancoFormPage.vue`

**Context:** The create form uses raw `<input>` and `<select>` elements for `codigoCompe`, `razaoSocial`, `apelido`, `padraoAntecipacao`.

**Changes:** Replace each with `<Input>` or `<Select>` from DS:
```html
<!-- BEFORE (Input) -->
<label class="form-field form-field--full">
  <span class="form-field__label">Código COMPE *</span>
  <input v-model="createForm.codigoCompe" type="text" class="form-field__input" ... />
  <span v-if="createErrors.codigoCompe" class="form-field__error">{{ createErrors.codigoCompe }}</span>
</label>

<!-- AFTER -->
<Input
  v-model="createForm.codigoCompe"
  label="Código COMPE"
  required
  :error="createErrors.codigoCompe ?? ''"
  full-width
/>
```

```html
<!-- BEFORE (Select) -->
<label class="form-field">
  <span class="form-field__label">Padrão Antecipação *</span>
  <select v-model="createForm.padraoAntecipacao" class="form-field__input">...</select>
  <span v-if="createErrors.padraoAntecipacao" class="form-field__error">...</span>
</label>

<!-- AFTER -->
<Select
  v-model="createForm.padraoAntecipacao"
  label="Padrão Antecipação"
  required
  :options="padraoOptions"
  :error="createErrors.padraoAntecipacao ?? ''"
/>
```

Add `Input` and `Select` to DS imports.

**Acceptance criteria:**
- Create form uses only DS `Input` and `Select` components
- Validation errors display via DS `:error` prop
- No raw `<input>` or `<select>` in create form section
- Remove `.form-field__input`, `.form-field__label`, `.form-field__error` CSS classes (if unused elsewhere)

**Size:** M — 1 file, 4 fields + CSS cleanup

---

### Task 2.3 — Replace native fields in edit form with DS components

**File:** `src/features/bancos/pages/BancoFormPage.vue`

**Context:** The edit form uses native `<select>`, `<input type="number">`, and `<textarea>` for `padraoAntecipacao`, numeric fields (avisoPrevio, valorMinimo, breakFunding, tla, etc.), and `observacoesAntecipacao`.

**Changes:**
- `<select>` for padraoAntecipacao → `<Select>` from DS with `:options="padraoOptions"`
- `<input type="number">` fields → `<Input type="number">` from DS
- `<textarea>` for observações → `<Textarea>` from DS (import `Textarea` from DS)

**Acceptance criteria:**
- Edit form uses only DS form components
- `Textarea` imported from `@nordware/design-system`
- Remove all `.form-field__*` native CSS classes from the component styles

**Size:** M — 1 file, 7 fields + CSS cleanup

---

## Phase 3 — ContratoDetailPage Dropdown

### Task 3.1 — Replace custom export dropdown with DS `Dropdown`

**File:** `src/features/contratos/pages/ContratoDetailPage.vue`

**Context:** The export menu is currently a hand-rolled div with `showExportMenu` state, `handleExportClickOutside`, `onMounted`/`onUnmounted` event listeners, and custom CSS classes `.export-dropdown__*`.

The DS exports a `Dropdown` component. From the Storybook stories, `Dropdown` accepts:
- `:items` — array of `{ key: string, label: string, icon?: string, disabled?: boolean }`
- `placement?` — `'bottom-start' | 'bottom-end' | 'top-start' | 'top-end'`
- `#trigger` slot — the element that opens the dropdown
- emits `@select(key: string)` when an item is clicked

**Changes:**
```html
<!-- BEFORE: custom implementation -->
<div ref="exportDropdownRef" class="export-dropdown">
  <Button variant="secondary" @click="showExportMenu = !showExportMenu">Exportar</Button>
  <div v-if="showExportMenu" class="export-dropdown__menu">
    <button @click="exportar('pdf')">Exportar PDF</button>
    <button @click="exportar('xlsx')">Exportar XLSX</button>
  </div>
</div>

<!-- AFTER: DS Dropdown -->
<Dropdown
  placement="bottom-end"
  :items="[
    { key: 'pdf',  label: 'Exportar PDF'  },
    { key: 'xlsx', label: 'Exportar XLSX' },
  ]"
  @select="(key) => exportar(key as 'pdf' | 'xlsx')"
>
  <template #trigger>
    <Button variant="secondary" size="md" icon-left="i-carbon-export">
      Exportar
    </Button>
  </template>
</Dropdown>
```

Remove: `showExportMenu`, `exportDropdownRef`, `handleExportClickOutside`, `onMounted`/`onUnmounted` imports (if no longer needed), `.export-dropdown__*` CSS classes.

**Acceptance criteria:**
- `Dropdown` imported from `@nordware/design-system`
- No `showExportMenu` ref in the component
- No `onMounted`/`onUnmounted` for click-outside handling (unless used elsewhere)
- Export menu closes correctly on outside click (handled by DS)

**Size:** M — 1 file, replaces ~30 lines of code

---

## Phase 4 — Theme and Cleanup

### Task 4.1 — Apply explicit `dark-theme` class

**File:** `src/app/App.vue` (root `<div>`)

**Change:**
```html
<!-- BEFORE -->
<div>
  <RouterView />
  ...
</div>

<!-- AFTER -->
<div class="dark-theme">
  <RouterView />
  ...
</div>
```

**Acceptance criteria:**
- `<body>` or the root `<div>` has `class="dark-theme"`
- Theme does not change when OS switches light/dark preference

**Size:** XS — 1-line change

---

### Task 5.1 — Remove orphan `src/style.css`

**File:** `src/style.css`

**Context:** This file is a Vite scaffold artifact and is NOT imported by `main.ts`. It defines duplicate `--shadow`, `--bg`, `--text` etc. that conflict with DS aliases.

**Changes:**
1. Confirm: `grep -r "style.css" src/` — expect no results
2. Delete `src/style.css`

**Acceptance criteria:**
- `src/style.css` no longer exists
- Build passes (`npm run build` exits 0)

**Size:** XS — 1 file deletion

---

## Checkpoint: After Phase 1

- [ ] All tests pass: `npm test`
- [ ] Build succeeds: `npm run build`
- [ ] `grep -r "color-danger\|color-text-muted\|color-surface-raised\|\"--color-text\"" src/` returns no results
- [ ] Visual check: painel, bancos, simulador pages render with correct DS dark-theme colors

## Checkpoint: After Phase 2

- [ ] `BancoFormPage.vue` create and edit forms use only DS components
- [ ] No `<input type="checkbox">` or unadorned `<select>` in `BancoFormPage.vue`
- [ ] All form tests pass

## Checkpoint: After Phase 3

- [ ] Export dropdown in `ContratoDetailPage.vue` uses DS `Dropdown`
- [ ] No custom click-outside listener for dropdown
- [ ] Full test suite passes

## Checkpoint: After Phase 4

- [ ] Root element has `dark-theme` class
- [ ] `src/style.css` deleted
- [ ] Final build and test run clean

---

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| DS `Dropdown` API differs from Storybook docs | Medium | Read the DS `Dropdown` type definitions directly from `node_modules/@nordware/design-system/dist/index.d.ts` before implementing Task 3.1 |
| `Textarea` prop API unknown | Low | Check DS type definitions before Task 2.3 |
| Phase 2 CSS cleanup breaks shared `.form-field__*` styles | Medium | Remove the CSS classes only after confirming nothing else in `BancoFormPage.vue` uses them |
| Removing style.css hides a legitimate global style | Low | Run `grep -r "style.css" src/` to confirm it is completely orphaned before deleting |

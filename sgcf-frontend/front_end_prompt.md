# SGCF Frontend — Final Implementation Prompt

## Mission

Complete the remaining frontend work on **SGCF (Sistema de Gestão de Contratos de Financiamento)** by fixing CSS token names, migrating native HTML form controls to `@nordware/design-system` components, replacing a custom dropdown with the DS `Dropdown`, and finalizing theme/cleanup tasks. Every change below is fully specified — execute them exactly as written, then run the verification checklist at the end. No exploration, no reinterpretation, no scope expansion.

---

## Project Facts

- **Name:** SGCF — Sistema de Gestão de Contratos de Financiamento
- **Stack:** Vue 3 + TypeScript strict (`exactOptionalPropertyTypes: true`, `noUncheckedIndexedAccess: true`), Vite 5, Pinia, TanStack Vue Query v5, Vue Router 4
- **Design System:** `@nordware/design-system` (local file dep at `../sgcf-backend/nordware-design-system`). Already installed. CSS already imported via `@nordware/design-system/styles` in `main.ts`.
- **Working directory:** `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-frontend`

---

## Invariants — Never Violate These

1. **Never** suppress TypeScript errors with `// @ts-ignore`, `// @ts-expect-error`, or `// eslint-disable`. Fix the root cause.
2. **Never** add new npm dependencies. The DS already exports everything needed.
3. **Never** create new utility files, helpers, or composables unless this prompt explicitly says to.
4. **Never** refactor code that is not in the task list. Surgical precision only.
5. **Never** remove a CSS class without first grepping the template to confirm it is unreferenced.
6. **Never** use deprecated token names. The only valid tokens are in the table below.
7. **Never** pass `string | undefined` to a DS `error` prop. With `exactOptionalPropertyTypes: true`, coerce with `|| ''`.
8. **Never** use the DS `Input` with `type="date"` — it is unsupported. Use the existing `.native-date-field` pattern (see `ContratoCreatePage.vue`) when a date field is needed (not required by any task here).
9. **Never** use `apiClient.post(...)` for create/mutation POSTs. Use `postIdempotent` from `@/shared/api/client`.
10. **Always** read a file before editing it. Always prefer `Edit` (diff) over `Write` (full rewrite) for existing files.
11. **Run `npm test` and `npm run build` only once, after all tasks are complete.**

---

## Design System — CSS Token Reference (the only valid names)

| Token | Purpose |
|---|---|
| `--color-background` | Page background |
| `--color-surface` | Card / panel surface |
| `--color-surface-elevated` | Raised surface (NOT `--color-surface-raised`) |
| `--color-text-primary` | Main text (NOT `--color-text`) |
| `--color-text-secondary` | Muted text (NOT `--color-text-muted`) |
| `--color-text-tertiary` | Extra muted text |
| `--color-error` | Error / danger (NOT `--color-danger`) |
| `--color-success` | Success |
| `--color-warning` | Warning |
| `--color-info` | Info |
| `--color-primary` | Brand accent (OKLCH) |
| `--color-hover` | Hover overlay |
| `--color-border` | Default border |
| `--color-border-hover` | Hover border |
| `--font-family-base` | Base font family |
| `--font-family-mono` | Monospace font family |
| `--radius-sm` / `--radius-md` / `--radius-lg` | Border radius scale |
| `--duration-fast` | Fast transition duration |
| `--shadow-sm` / `--shadow-md` / `--shadow-lg` | Elevation scale |

**Forbidden tokens (must not appear anywhere in `src/`):** `--color-danger`, `--color-text-muted`, `--color-text` (as a standalone token), `--color-surface-raised`.

---

## Design System — Component API Cheat Sheet

All components are named exports from `@nordware/design-system`.

**Available exports**
- Atoms: `Avatar`, `Badge`, `Button`, `Checkbox`, `HealthCircle`, `Input`, `Progress`, `Radio`, `Spinner`, `Textarea`
- Molecules: `Alert`, `Breadcrumb`, `Card`, `DataTable`, `Dropdown`, `EmptyState`, `FlywheelPhase`, `Pagination`, `Select`, `Skeleton`, `Tabs`, `Tooltip`
- Organisms: `Modal`
- Templates: `DashboardGrid`, `PageHeader`, `PageLayout`
- Extras: `AnomalyPanel`, `CommandPalette`, `OrderCard`, `Toast`, `ToastContainer`

### Badge
`variant: 'default' | 'primary' | 'info' | 'success' | 'warning' | 'danger'`, `size: 'sm' | 'md' | 'lg'`, `pill`, `outline`.

### Input
Props: `modelValue`, `type` (no `'date'`), `label`, `placeholder`, `helper`, `error: string` (optional), `disabled`, `required`, `fullWidth` (template attr: `full-width`), `iconLeft` (template attr: `icon-left`).

### Select
Props: `modelValue`, `options: SelectOption[]` where `SelectOption = { label: string; value: string | number; disabled?: boolean }`, `label`, `placeholder`, `helper`, `error: string` (optional), `disabled`.
Coerces empty string via `Number('') = 0` — pass string values and use `''` for "no selection".

### Checkbox
Props: `modelValue: boolean`, `label: string`, `disabled`, `indeterminate`, `size`, `error: string` (optional). Use with `v-model`.

### Textarea
Props: `modelValue: string`, `label`, `placeholder`, `helper`, `error: string` (optional), `disabled`, `required`, `rows`, `resize`. Use with `v-model`.

### Dropdown
Props: `items: DropdownItem[]` where `DropdownItem = { key: string; label: string; icon?: string; disabled?: boolean; separator?: boolean }`, `placement: 'bottom-start' | 'bottom-end' | 'top-start' | 'top-end'` (default `'bottom-start'`), `disabled`.
Slot: `#trigger`. Emits: `select(key: string)`.

### Card
Props: `title?: string`, `subtitle?: string`, `padding?: 'none' | 'sm' | 'md' | 'lg'`, `hoverable`, `bordered`. Slots: `header`, default, `footer`.

### Modal
`v-model: boolean`, `title: string`, `size: 'sm' | 'md' | 'lg' | 'xl'`, `closeOnOverlay`. Slots: default, `footer`.

### Tabs
`modelValue: string` (v-model), `items: { key: string; label: string; icon?: string; disabled?: boolean }[]`. Emits `update:modelValue`.

### DataTable
`columns: Column<T>[]` where `Column<T> = { key: keyof T; label: string; sortable?: boolean; width?: string; align?: 'left' | 'center' | 'right' }`, `data: T[]`, `loading?: boolean`. Cell slot: `#cell-{key}="{ row, value }"`.

### Pagination
`currentPage: number`, `totalPages: number`, `maxVisible?: number`. Emits `update:currentPage`.

### PageLayout
Props: `maxWidth: 'narrow' | 'default' | 'wide' | 'full'`, `hasSidebar` (template: `has-sidebar`), `sidebarWidth` (template: `sidebar-width`), `stickyHeader`. Slots: `header`, `sidebar`, default, `footer`.

### PageHeader
Props: `title: string`, `description?: string`. Slots: `actions`, `tabs`.

### DashboardGrid
Props: `columns: 1 | 2 | 3 | 4 | 6 | 12`, `gap: 'none' | 'sm' | 'md' | 'lg'`, `responsive: boolean`.

### Alert
`variant: 'info' | 'success' | 'warning' | 'error'` (NOT `'danger'`), `title?: string`. Default slot for message.

---

## Established Patterns (follow exactly)

**Idempotent POSTs**
```typescript
import { postIdempotent } from '@/shared/api/client'
await postIdempotent<ResponseType>(API.resource.create, body)
```

**Error props under strict TS**
```typescript
// DS error prop is `error?: string`. Never pass `string | undefined`.
:error="(touched && errors['field']) || ''"
```

**Local type aliases (DS does not re-export these)**
```typescript
interface Column<T> { key: keyof T; label: string; sortable?: boolean; align?: 'left' | 'center' | 'right' }
interface SelectOption { label: string; value: string | number; disabled?: boolean }
```

**API client imports**
```typescript
import { apiClient, postIdempotent, extractApiError } from '@/shared/api/client'
```

**Bancos options**
```typescript
import { useBancosOptions } from '@/shared/api/useBancosOptions'
```

**Toast**
```typescript
import { toast } from '@/shared/ui/toast'
toast.success('message')
toast.error('message')
```

---

## Tasks — Execute in This Order

### Task A — CSS Token Fixes

For each file below, use the `Edit` tool with `replace_all: true` when the same wrong token appears more than once.

#### A1. `src/features/painel/pages/KpisExecutivoPage.vue`
- Replace `var(--color-danger, #dc2626)` → `var(--color-error)`
- Replace `var(--color-text-muted, #6b7280)` → `var(--color-text-secondary)`

#### A2. `src/features/bancos/pages/BancoDetailPage.vue`
- Replace `var(--color-text-muted, #6b7280)` → `var(--color-text-secondary)`
- Replace `var(--color-text, #111827)` → `var(--color-text-primary)`

#### A3. `src/features/painel/pages/PainelGarantiasPage.vue`
- Replace `var(--color-text-muted, #6b7280)` → `var(--color-text-secondary)`

#### A4. `src/features/simulador/pages/AntecipacaoPortfolioPage.vue`
- Replace `var(--color-surface-raised, var(--color-surface))` → `var(--color-surface-elevated)`

#### A5. `src/features/simulador/pages/CenarioCambialPage.vue`
- Replace `var(--color-surface-raised, var(--color-surface))` → `var(--color-surface-elevated)`
- If `var(--color-text-muted, #6b7280)` is present, replace with `var(--color-text-secondary)`

**Acceptance for Task A:** `grep -r "color-danger\|color-text-muted\|color-surface-raised" src/` returns nothing.

---

### Task B — `BancoFormPage.vue` Migration to DS Components

**File:** `src/features/bancos/pages/BancoFormPage.vue`

Read the file fully before editing. Add to the existing `@nordware/design-system` import (or create one if missing): `Input`, `Select`, `Checkbox`, `Textarea`.

#### B1. Edit form — Replace 4 native checkboxes with DS `Checkbox`

Replace the 4 native `<input type="checkbox">` blocks (with their custom label wrappers) with:

```html
<Checkbox v-model="editForm.aceitaLiquidacaoTotal"   label="Aceita Liquidação Total" />
<Checkbox v-model="editForm.aceitaLiquidacaoParcial" label="Aceita Liquidação Parcial" />
<Checkbox v-model="editForm.exigeAnuenciaExpressa"   label="Exige Anuência Expressa" />
<Checkbox v-model="editForm.exigeParcelaInteira"     label="Exige Parcela Inteira" />
```

#### B2. Create form — Replace 4 native inputs with DS `Input` / `Select`

```html
<Input
  v-model="createForm.codigoCompe"
  label="Código COMPE"
  required
  :error="createErrors.codigoCompe || ''"
  full-width
/>
<Input
  v-model="createForm.razaoSocial"
  label="Razão Social"
  required
  :error="createErrors.razaoSocial || ''"
  full-width
/>
<Input
  v-model="createForm.apelido"
  label="Apelido"
  required
  :error="createErrors.apelido || ''"
  full-width
/>
<Select
  v-model="createForm.padraoAntecipacao"
  label="Padrão Antecipação"
  required
  :options="padraoOptions"
  :error="createErrors.padraoAntecipacao || ''"
/>
```

#### B3. Edit form — Replace 7 native fields

```html
<Select
  v-model="editForm.padraoAntecipacao"
  label="Padrão Antecipação"
  :options="padraoOptions"
/>

<Input
  v-model="editForm.avisoPrevioMinDiasUteis"
  type="number"
  label="Aviso Prévio Mín. (dias úteis)"
/>

<Input
  :model-value="editForm.valorMinimoParcialPct"
  type="number"
  label="Valor Mínimo Parcial (%)"
  placeholder="Deixe em branco para não definir"
  @update:model-value="(v) => editForm.valorMinimoParcialPct = String(v)"
/>

<!-- breakFundingFeePct, tlaPctSobreSaldo, tlaPctPorMesRemanescente — same pattern as valorMinimoParcialPct -->

<Textarea
  v-model="editForm.observacoesAntecipacao"
  label="Observações Antecipação"
  :rows="3"
  placeholder="Observações opcionais..."
/>
```

#### B4. CSS cleanup

After migrating, remove these classes from the `<style scoped>` block **only if** a `grep` of the template confirms they are no longer referenced:
- `.form-field__input`
- `.form-field__label`
- `.form-field__error`
- `.form-field__required`
- `.form-field__textarea`
- `.form-field--checkbox`
- `.checkbox-label`
- `.checkbox-input`

**Acceptance for Task B:**
- `grep -n 'input type="checkbox"' src/features/bancos/pages/BancoFormPage.vue` returns nothing.
- `grep -nE '<(input|select|textarea) ' src/features/bancos/pages/BancoFormPage.vue` returns nothing.
- No removed class still appears in the template.

---

### Task C — `ContratoDetailPage.vue` — Replace custom dropdown with DS `Dropdown`

**File:** `src/features/contratos/pages/ContratoDetailPage.vue`

Read the file fully before editing.

#### C1. Template — Replace the custom export dropdown markup with:

```html
<Dropdown
  placement="bottom-end"
  :items="[
    { key: 'pdf',  label: 'Exportar PDF'  },
    { key: 'xlsx', label: 'Exportar XLSX' },
  ]"
  @select="(key: string) => exportar(key as 'pdf' | 'xlsx')"
>
  <template #trigger>
    <Button variant="secondary" size="md" icon-left="i-carbon-export">
      Exportar
    </Button>
  </template>
</Dropdown>
```

#### C2. Script — Remove

- `showExportMenu` ref
- `exportDropdownRef` ref
- `handleExportClickOutside` function
- `onMounted` / `onUnmounted` imports **only if** they are not used elsewhere in this file (grep before removing).

#### C3. Imports

Add `Dropdown` to the existing `@nordware/design-system` import.

#### C4. CSS — Remove (only if no longer referenced in the template)

- `.export-dropdown`
- `.export-dropdown__menu`
- `.export-dropdown__item`

**Acceptance for Task C:** `grep -n "showExportMenu\|exportDropdownRef\|handleExportClickOutside" src/features/contratos/pages/ContratoDetailPage.vue` returns nothing.

---

### Task D — Theme & Cleanup

#### D1. `src/app/App.vue`
Add `class="dark-theme"` to the root `<div>` so the theme is deterministic. If the root already has a `class` attribute, append `dark-theme` to the existing class list — do not replace it.

#### D2. `src/style.css`
Run `grep -r "style.css" src/`. If the file is **not** imported anywhere, delete it. If it is imported, leave it in place and skip this step.

**Acceptance for Task D:**
- `src/app/App.vue` root element contains `dark-theme` in its class list.
- `src/style.css` either no longer exists or is still referenced from somewhere in `src/`.

---

## Verification Checklist (run once, after all tasks)

Execute each of the following and confirm the expected result:

1. **Tests**
   ```bash
   npm test
   ```
   Expected: all 53 tests pass, zero failures.

2. **Production build**
   ```bash
   npm run build
   ```
   Expected: exit code 0, zero TypeScript errors.

3. **No forbidden CSS tokens remain**
   ```bash
   grep -r "color-danger\|color-text-muted\|color-surface-raised" src/
   ```
   Expected: no output.

4. **No native checkboxes remain in `BancoFormPage.vue`**
   ```bash
   grep -n 'input type="checkbox"' src/features/bancos/pages/BancoFormPage.vue
   ```
   Expected: no output.

5. **No dropdown leftovers in `ContratoDetailPage.vue`**
   ```bash
   grep -n "showExportMenu\|exportDropdownRef\|handleExportClickOutside" src/features/contratos/pages/ContratoDetailPage.vue
   ```
   Expected: no output.

If any verification step fails, fix the root cause (do not suppress) and re-run the full checklist. Only report completion when every step passes.

---

## Final Report Format

After verification passes, output:

1. List of files modified (absolute paths).
2. List of files deleted (if any).
3. `npm test` summary line (e.g., "53 passed").
4. `npm run build` exit status.
5. The exact output of each grep verification command.

No other commentary.

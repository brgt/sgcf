<script setup lang="ts">
import { ref, watch } from 'vue'
import type { BancoDto } from '@/shared/api/types'
import { Input, Select, Button } from '@nordware/design-system'
import { ModalidadeContrato, Moeda } from '@/shared/api/enums'
import type { MutableContratoFilters } from '@/features/contratos/api/useContratos'

// Local type alias — Column/SelectOption are not re-exported by the DS root index
interface SelectOption {
  label: string
  value: string | number
  disabled?: boolean
}

// ---------------------------------------------------------------------------
// Props / emits
// ---------------------------------------------------------------------------

interface Props {
  filters: MutableContratoFilters
  bancos: BancoDto[]
}

const props = defineProps<Props>()

const emit = defineEmits<{
  update: [patch: Partial<MutableContratoFilters>]
  reset: []
}>()

// ---------------------------------------------------------------------------
// Local mirror of debounced fields (search, valorMin, valorMax)
// ---------------------------------------------------------------------------

const localSearch = ref(props.filters.search)
const localValorMin = ref<number | ''>(props.filters.valorPrincipalMin || '')
const localValorMax = ref<number | ''>(props.filters.valorPrincipalMax || '')

// Sync when parent resets filters
watch(
  () => props.filters.search,
  (v) => { localSearch.value = v },
)
watch(
  () => props.filters.valorPrincipalMin,
  (v) => { localValorMin.value = v || '' },
)
watch(
  () => props.filters.valorPrincipalMax,
  (v) => { localValorMax.value = v || '' },
)

// ---------------------------------------------------------------------------
// Debounced input handlers (text/number inputs)
// ---------------------------------------------------------------------------

let searchTimer: ReturnType<typeof setTimeout> | null = null
let valorMinTimer: ReturnType<typeof setTimeout> | null = null
let valorMaxTimer: ReturnType<typeof setTimeout> | null = null

function onSearchInput(value: string | number): void {
  localSearch.value = String(value)
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    emit('update', { search: localSearch.value })
  }, 300)
}

function onValorMinInput(value: string | number): void {
  localValorMin.value = value === '' ? '' : Number(value)
  if (valorMinTimer) clearTimeout(valorMinTimer)
  valorMinTimer = setTimeout(() => {
    emit('update', { valorPrincipalMin: localValorMin.value === '' ? 0 : Number(localValorMin.value) })
  }, 300)
}

function onValorMaxInput(value: string | number): void {
  localValorMax.value = value === '' ? '' : Number(value)
  if (valorMaxTimer) clearTimeout(valorMaxTimer)
  valorMaxTimer = setTimeout(() => {
    emit('update', { valorPrincipalMax: localValorMax.value === '' ? 0 : Number(localValorMax.value) })
  }, 300)
}

// ---------------------------------------------------------------------------
// Immediate handlers (dropdowns / date inputs)
// ---------------------------------------------------------------------------

// The Select component coerces empty-string values to 0 (Number('')=0),
// so we normalise back to '' when we get 0 from a string-valued select.
function normaliseSelectString(value: string | number): string {
  return value === 0 ? '' : String(value)
}

function onBancoChange(value: string | number): void {
  emit('update', { bancoId: normaliseSelectString(value) })
}

function onModalidadeChange(value: string | number): void {
  emit('update', { modalidade: normaliseSelectString(value) })
}

function onMoedaChange(value: string | number): void {
  emit('update', { moeda: normaliseSelectString(value) })
}

function onStatusChange(value: string | number): void {
  emit('update', { status: normaliseSelectString(value) })
}

function onDataDeChange(event: Event): void {
  const target = event.target as HTMLInputElement
  emit('update', { dataVencimentoDe: target.value })
}

function onDataAteChange(event: Event): void {
  const target = event.target as HTMLInputElement
  emit('update', { dataVencimentoAte: target.value })
}

function onTemHedgeChange(value: string | number): void {
  emit('update', { temHedge: normaliseSelectString(value) })
}

function onTemGarantiaChange(value: string | number): void {
  emit('update', { temGarantia: normaliseSelectString(value) })
}

function onTemAlertaChange(value: string | number): void {
  emit('update', { temAlertaVencimento: normaliseSelectString(value) })
}

// ---------------------------------------------------------------------------
// Select option lists (static — built once)
// ---------------------------------------------------------------------------

const modalidadeOptions: SelectOption[] = [
  { label: 'Todas', value: '' },
  { label: 'Finimp', value: ModalidadeContrato.Finimp },
  { label: 'Refinimp', value: ModalidadeContrato.Refinimp },
  { label: 'Lei 4131', value: ModalidadeContrato.Lei4131 },
  { label: 'NCE', value: ModalidadeContrato.Nce },
  { label: 'Balcão Caixa', value: ModalidadeContrato.BalcaoCaixa },
  { label: 'FGI', value: ModalidadeContrato.Fgi },
]

const moedaOptions: SelectOption[] = [
  { label: 'Todas', value: '' },
  { label: 'BRL', value: Moeda.Brl },
  { label: 'USD', value: Moeda.Usd },
  { label: 'EUR', value: Moeda.Eur },
  { label: 'JPY', value: Moeda.Jpy },
  { label: 'CNY', value: Moeda.Cny },
]

const statusOptions: SelectOption[] = [
  { label: 'Todos', value: '' },
  { label: 'Ativo', value: 'Ativo' },
  { label: 'Liquidado', value: 'Liquidado' },
  { label: 'Vencido', value: 'Vencido' },
  { label: 'Inadimplente', value: 'Inadimplente' },
  { label: 'Cancelado', value: 'Cancelado' },
  { label: 'Refinanciado Parcial', value: 'RefinanciadoParcial' },
  { label: 'Refinanciado Total', value: 'RefinanciadoTotal' },
]

const booleanOptions: SelectOption[] = [
  { label: 'Todos', value: '' },
  { label: 'Sim', value: 'true' },
  { label: 'Não', value: 'false' },
]
</script>

<template>
  <div class="filter-panel">
    <div class="filter-panel__header">
      <span class="filter-panel__title">Filtros</span>
      <Button
        variant="ghost"
        size="sm"
        icon-left="i-carbon-reset"
        @click="emit('reset')"
      >
        Limpar
      </Button>
    </div>

    <div class="filter-panel__fields">
      <!-- Search -->
      <Input
        :model-value="localSearch"
        label="Buscar"
        placeholder="Nº externo, código interno..."
        icon-left="i-carbon-search"
        full-width
        @update:model-value="onSearchInput"
      />

      <!-- Banco -->
      <Select
        :model-value="filters.bancoId"
        label="Banco"
        :options="[
          { label: 'Todos', value: '' },
          ...props.bancos.map(b => ({ label: b.apelido, value: b.id }))
        ]"
        placeholder="Todos os bancos"
        @update:model-value="onBancoChange"
      />

      <!-- Modalidade -->
      <Select
        :model-value="filters.modalidade"
        label="Modalidade"
        :options="modalidadeOptions"
        @update:model-value="onModalidadeChange"
      />

      <!-- Moeda -->
      <Select
        :model-value="filters.moeda"
        label="Moeda"
        :options="moedaOptions"
        @update:model-value="onMoedaChange"
      />

      <!-- Status -->
      <Select
        :model-value="filters.status"
        label="Status"
        :options="statusOptions"
        @update:model-value="onStatusChange"
      />

      <!-- Data Vencimento De/Até -->
      <div class="filter-panel__date-range">
        <div class="filter-panel__date-field">
          <label class="filter-panel__date-label">Vencimento de</label>
          <input
            type="date"
            class="filter-panel__date-input"
            :value="filters.dataVencimentoDe"
            @change="onDataDeChange"
          />
        </div>
        <div class="filter-panel__date-field">
          <label class="filter-panel__date-label">Vencimento até</label>
          <input
            type="date"
            class="filter-panel__date-input"
            :value="filters.dataVencimentoAte"
            @change="onDataAteChange"
          />
        </div>
      </div>

      <!-- Valor Principal Min/Max -->
      <Input
        :model-value="localValorMin"
        type="number"
        label="Valor mínimo (R$)"
        placeholder="0"
        full-width
        @update:model-value="onValorMinInput"
      />
      <Input
        :model-value="localValorMax"
        type="number"
        label="Valor máximo (R$)"
        placeholder="Sem limite"
        full-width
        @update:model-value="onValorMaxInput"
      />

      <!-- Tem Hedge -->
      <Select
        :model-value="filters.temHedge"
        label="Tem hedge?"
        :options="booleanOptions"
        @update:model-value="onTemHedgeChange"
      />

      <!-- Tem Garantia -->
      <Select
        :model-value="filters.temGarantia"
        label="Tem garantia?"
        :options="booleanOptions"
        @update:model-value="onTemGarantiaChange"
      />

      <!-- Tem Alerta de Vencimento -->
      <Select
        :model-value="filters.temAlertaVencimento"
        label="Alerta de vencimento?"
        :options="booleanOptions"
        @update:model-value="onTemAlertaChange"
      />
    </div>
  </div>
</template>

<style scoped>
.filter-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--color-surface);
}

.filter-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 1rem 0.75rem;
  border-bottom: 1px solid var(--color-border);
}

.filter-panel__title {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-primary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.filter-panel__fields {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1rem;
  overflow-y: auto;
  flex: 1;
}

.filter-panel__date-range {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-panel__date-field {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.filter-panel__date-label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.filter-panel__date-input {
  width: 100%;
  padding: 0.625rem 1rem;
  font-family: var(--font-family-base);
  font-size: 1rem;
  color: var(--color-text-primary);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  transition: border-color var(--duration-fast);
  outline: none;
}

.filter-panel__date-input:hover {
  border-color: var(--color-border-hover);
}

.filter-panel__date-input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(0, 249, 184, 0.1);
}
</style>

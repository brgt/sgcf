<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  DataTable,
  Pagination,
  PageLayout,
  PageHeader,
  Badge,
  EmptyState,
  Skeleton,
  Button,
} from '@nordware/design-system'
import RoleGate from '@/shared/auth/RoleGate.vue'
import ContratoFilterPanel from '@/features/contratos/components/ContratoFilterPanel.vue'
import { useBancosOptions } from '@/shared/api/useBancosOptions'
import {
  listContratos,
  CONTRATO_FILTER_DEFAULTS,
  type MutableContratoFilters,
} from '@/features/contratos/api/useContratos'
import { useUrlFilters } from '@/shared/filters/useUrlFilters'
import { usePagedList } from '@/shared/filters/usePagedList'
import { formatMoney } from '@/shared/money/formatMoney'
import { formatLocalDate } from '@/shared/dates/formatDate'
import type { MoedaCode } from '@/shared/money/Money'

// Local type alias — Column<T> is not re-exported by the DS root index
interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  width?: string
  align?: 'left' | 'center' | 'right'
}

// ============================================================================
// Router
// ============================================================================

const router = useRouter()

// ============================================================================
// Filters — URL-synced
// ============================================================================

const { filters, setFilter, setFilters, resetFilters } = useUrlFilters(CONTRATO_FILTER_DEFAULTS)

// ============================================================================
// Bancos
// ============================================================================

const bancosQuery = useBancosOptions()
const bancos = computed(() => bancosQuery.data.value ?? [])

/** Map bancoId → apelido for display in the table */
const bancosMap = computed<Map<string, string>>(() => {
  const map = new Map<string, string>()
  for (const banco of bancos.value) {
    map.set(banco.id, banco.apelido)
  }
  return map
})

// ============================================================================
// Contratos list query
// ============================================================================

const {
  items: contratoItems,
  totalPages,
  currentPage,
  isFetching,
  isEmpty,
} = usePagedList({
  queryKey: ['contratos'] as const,
  fetchFn: listContratos,
  filters,
})

// ============================================================================
// Display DTO — flattens ContratoDto into a table-friendly row
// ============================================================================

interface ContratoRow {
  id: string
  numeroCodigo: string
  bancoNome: string
  modalidade: string
  moeda: string
  valorPrincipalFmt: string
  dataVencimentoFmt: string
  status: string
}

const tableRows = computed<ContratoRow[]>(() =>
  contratoItems.value.map((c) => ({
    id: c.id,
    numeroCodigo: c.codigoInterno
      ? `${c.numeroExterno} / ${c.codigoInterno}`
      : c.numeroExterno,
    bancoNome: bancosMap.value.get(c.bancoId) ?? c.bancoId,
    modalidade: modalidadeLabel(c.modalidade),
    moeda: c.moeda,
    valorPrincipalFmt: formatMoney(c.valorPrincipal, c.moeda as MoedaCode),
    dataVencimentoFmt: formatLocalDate(c.dataVencimento),
    status: c.status,
  })),
)

const columns: Column<ContratoRow>[] = [
  { key: 'numeroCodigo',      label: 'Nº / Código',     sortable: false },
  { key: 'bancoNome',         label: 'Banco',            sortable: false },
  { key: 'modalidade',        label: 'Modalidade',       sortable: false },
  { key: 'moeda',             label: 'Moeda',            sortable: false },
  { key: 'valorPrincipalFmt', label: 'Valor Principal',  sortable: false, align: 'right' },
  { key: 'dataVencimentoFmt', label: 'Vencimento',       sortable: false },
  { key: 'status',            label: 'Status',           sortable: false },
]

// ============================================================================
// Helpers
// ============================================================================

function modalidadeLabel(m: string): string {
  const labels: Record<string, string> = {
    Finimp: 'Finimp',
    Refinimp: 'Refinimp',
    Lei4131: 'Lei 4131',
    Nce: 'NCE',
    BalcaoCaixa: 'Balcão Caixa',
    Fgi: 'FGI',
  }
  return labels[m] ?? m
}

type BadgeVariant = 'default' | 'primary' | 'success' | 'warning' | 'danger' | 'info'

function statusVariant(status: string): BadgeVariant {
  const map: Record<string, BadgeVariant> = {
    Ativo: 'success',
    Liquidado: 'info',
    Vencido: 'danger',
    Inadimplente: 'danger',
    Cancelado: 'default',
    RefinanciadoParcial: 'warning',
    RefinanciadoTotal: 'warning',
  }
  return map[status] ?? 'default'
}

function statusLabel(status: string): string {
  const map: Record<string, string> = {
    Ativo: 'Ativo',
    Liquidado: 'Liquidado',
    Vencido: 'Vencido',
    Inadimplente: 'Inadimplente',
    Cancelado: 'Cancelado',
    RefinanciadoParcial: 'Refinanciado Parcial',
    RefinanciadoTotal: 'Refinanciado Total',
  }
  return map[status] ?? status
}

// ============================================================================
// Event handlers
// ============================================================================

function onFilterUpdate(patch: Partial<MutableContratoFilters>): void {
  setFilters(patch)
}

function onFilterReset(): void {
  resetFilters()
}

function onPageChange(page: number): void {
  setFilter('page', page)
}

function onRowClick(row: ContratoRow): void {
  void router.push(`/contratos/${row.id}`)
}
</script>

<template>
  <PageLayout has-sidebar sidebar-width="280px" max-width="full" padding="none">
    <template #header>
      <PageHeader title="Contratos">
        <template #actions>
          <RoleGate policy="Escrita">
            <Button
              variant="primary"
              size="md"
              icon-left="i-carbon-add"
              @click="() => void router.push('/contratos/novo')"
            >
              Novo Contrato
            </Button>
          </RoleGate>
        </template>
      </PageHeader>
    </template>

    <template #sidebar>
      <ContratoFilterPanel
        :filters="filters"
        :bancos="bancos"
        @update="onFilterUpdate"
        @reset="onFilterReset"
      />
    </template>

    <!-- Main content area -->
    <div class="contratos-list">
      <!-- Loading skeleton (first load, no data yet) -->
      <template v-if="isFetching && contratoItems.length === 0">
        <div class="contratos-list__skeletons">
          <Skeleton v-for="n in 8" :key="n" height="48px" />
        </div>
      </template>

      <!-- Empty state (not loading, no results) -->
      <template v-else-if="isEmpty">
        <EmptyState
          icon="i-carbon-document"
          title="Nenhum contrato encontrado"
          description="Tente ajustar os filtros ou cadastre um novo contrato."
        />
      </template>

      <!-- Data table + pagination -->
      <template v-else>
        <DataTable
          :columns="columns"
          :data="tableRows"
          :loading="isFetching"
          @row-click="onRowClick"
        >
          <template #cell-status="{ value }">
            <Badge :variant="statusVariant(String(value))" pill size="sm">
              {{ statusLabel(String(value)) }}
            </Badge>
          </template>
        </DataTable>

        <div class="contratos-list__pagination">
          <Pagination
            :current-page="currentPage"
            :total-pages="totalPages"
            @update:current-page="onPageChange"
          />
        </div>
      </template>
    </div>
  </PageLayout>
</template>

<style scoped>
.contratos-list {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.contratos-list__skeletons {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.contratos-list__pagination {
  display: flex;
  justify-content: center;
  padding-top: 1rem;
}
</style>

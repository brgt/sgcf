<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import {
  DataTable,
  PageLayout,
  PageHeader,
  EmptyState,
  Skeleton,
  Button,
} from '@nordware/design-system'
import RoleGate from '@/shared/auth/RoleGate.vue'
import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { formatBrl } from '@/shared/money/formatMoney'
import type { BancoDto } from '@/shared/api/types'

// Local type alias — Column<T> is not re-exported by the DS root index
interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
}

// ============================================================================
// Router
// ============================================================================

const router = useRouter()

// ============================================================================
// Search with 300ms debounce
// ============================================================================

const search = ref('')
const debouncedSearch = ref('')

let debounceTimer: ReturnType<typeof setTimeout> | null = null

watch(search, (val) => {
  if (debounceTimer !== null) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    debouncedSearch.value = val
  }, 300)
})

// ============================================================================
// Query
// ============================================================================

const { data: bancos, isLoading } = useQuery({
  queryKey: computed(() => ['bancos', debouncedSearch.value]),
  queryFn: async (): Promise<BancoDto[]> => {
    const { data } = await apiClient.get<BancoDto[]>(API.bancos.list, {
      params: debouncedSearch.value ? { search: debouncedSearch.value } : {},
    })
    return data
  },
  staleTime: 60_000,
})

const items = computed<BancoDto[]>(() => bancos.value ?? [])
const isEmpty = computed(() => !isLoading.value && items.value.length === 0)

// ============================================================================
// Table rows
// ============================================================================

interface BancoRow {
  id: string
  apelido: string
  razaoSocial: string
  codigoCompe: string
  padraoAntecipacao: string
  limiteCreditoBrl: string
}

const PADRAO_LABELS: Record<string, string> = {
  LiquidacaoTotal:   'Liquidação Total',
  LiquidacaoParcial: 'Liquidação Parcial',
  AmbosOsModelos:    'Ambos os Modelos',
}

const tableRows = computed<BancoRow[]>(() =>
  items.value.map((b) => ({
    id: b.id,
    apelido: b.apelido,
    razaoSocial: b.razaoSocial,
    codigoCompe: b.codigoCompe,
    padraoAntecipacao: PADRAO_LABELS[b.padraoAntecipacao] ?? b.padraoAntecipacao,
    limiteCreditoBrl: b.limiteCreditoBrl !== null ? formatBrl(b.limiteCreditoBrl) : '—',
  })),
)

const columns: Column<BancoRow>[] = [
  { key: 'apelido',          label: 'Apelido',             sortable: false },
  { key: 'razaoSocial',      label: 'Razão Social',        sortable: false },
  { key: 'codigoCompe',      label: 'Cód. COMPE',          sortable: false },
  { key: 'padraoAntecipacao', label: 'Padrão Antecipação', sortable: false },
  { key: 'limiteCreditoBrl', label: 'Limite BRL',          sortable: false, align: 'right' },
]

// ============================================================================
// Handlers
// ============================================================================

function onRowClick(row: BancoRow): void {
  void router.push(`/bancos/${row.id}`)
}
</script>

<template>
  <PageLayout max-width="full">
    <template #header>
      <PageHeader title="Bancos">
        <template #actions>
          <RoleGate policy="Admin">
            <Button
              variant="primary"
              size="md"
              icon-left="i-carbon-add"
              @click="() => void router.push('/bancos/novo')"
            >
              Novo Banco
            </Button>
          </RoleGate>
        </template>
      </PageHeader>
    </template>

    <div class="bancos-list">
      <!-- Search -->
      <div class="bancos-list__toolbar">
        <input
          v-model="search"
          type="text"
          class="bancos-list__search"
          placeholder="Buscar por apelido ou razão social..."
          aria-label="Buscar bancos"
        />
      </div>

      <!-- Loading skeleton -->
      <template v-if="isLoading && items.length === 0">
        <div class="bancos-list__skeletons">
          <Skeleton v-for="n in 6" :key="n" height="48px" />
        </div>
      </template>

      <!-- Empty state -->
      <template v-else-if="isEmpty">
        <EmptyState
          icon="i-carbon-bank"
          title="Nenhum banco encontrado"
          description="Tente ajustar a busca ou cadastre um novo banco."
        />
      </template>

      <!-- Table -->
      <template v-else>
        <DataTable
          :columns="columns"
          :data="tableRows"
          :loading="isLoading"
          @row-click="onRowClick"
        />
      </template>
    </div>
  </PageLayout>
</template>

<style scoped>
.bancos-list {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.bancos-list__toolbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.bancos-list__search {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border, #d1d5db);
  border-radius: 6px;
  font-size: 0.875rem;
  outline: none;
  width: 320px;
  transition: border-color 0.15s;
}

.bancos-list__search:focus {
  border-color: var(--color-primary, #3b82f6);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
}

.bancos-list__skeletons {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
</style>

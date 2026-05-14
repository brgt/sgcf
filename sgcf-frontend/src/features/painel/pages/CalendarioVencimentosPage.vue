<script setup lang="ts">
import { ref, computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  Badge,
  Button,
  DataTable,
  Alert,
  Skeleton,
  EmptyState,
} from '@nordware/design-system'
import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { formatBrl } from '@/shared/money/formatMoney'
import type { CalendarioVencimentosDto, CalendarioMesDto } from '@/shared/api/types'

// ============================================================================
// Local type aliases
// ============================================================================

interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
}

// ============================================================================
// Table row
// ============================================================================

interface MesRow {
  mesNome: string
  totalBrl: string
  totalBrlRaw: number
  quantidadeParcelas: number
  isDestaque: boolean
}

// ============================================================================
// Year selector state
// ============================================================================

const currentYear = new Date().getFullYear()
const selectedYear = ref(currentYear)

const MIN_YEAR = currentYear - 2
const MAX_YEAR = currentYear + 3

function prevYear(): void {
  if (selectedYear.value > MIN_YEAR) {
    selectedYear.value--
  }
}

function nextYear(): void {
  if (selectedYear.value < MAX_YEAR) {
    selectedYear.value++
  }
}

// ============================================================================
// Query
// ============================================================================

const {
  data: calendario,
  isLoading,
  isError,
} = useQuery({
  queryKey: computed(() => ['painel', 'vencimentos', selectedYear.value] as const),
  queryFn: async (): Promise<CalendarioVencimentosDto> => {
    const { data } = await apiClient.get<CalendarioVencimentosDto>(API.painel.vencimentos, {
      params: { ano: selectedYear.value },
    })
    return data
  },
  staleTime: 60_000,
})

// ============================================================================
// Month names in Portuguese
// ============================================================================

const NOMES_MESES: Record<number, string> = {
  1:  'Janeiro',
  2:  'Fevereiro',
  3:  'Março',
  4:  'Abril',
  5:  'Maio',
  6:  'Junho',
  7:  'Julho',
  8:  'Agosto',
  9:  'Setembro',
  10: 'Outubro',
  11: 'Novembro',
  12: 'Dezembro',
}

// ============================================================================
// Computed table data
// ============================================================================

const sortedByValue = computed<number[]>(() => {
  const meses = calendario.value?.meses ?? []
  return [...meses]
    .sort((a, b) => b.totalBrl - a.totalBrl)
    .slice(0, 3)
    .map((m) => m.mes)
})

const mesRows = computed<MesRow[]>(() =>
  (calendario.value?.meses ?? []).map((linha: CalendarioMesDto) => ({
    mesNome: NOMES_MESES[linha.mes] ?? String(linha.mes),
    totalBrl: formatBrl(linha.totalBrl),
    totalBrlRaw: linha.totalBrl,
    quantidadeParcelas: linha.quantidadeParcelas,
    isDestaque: sortedByValue.value.includes(linha.mes),
  })),
)

const totalAnoFmt = computed(() =>
  calendario.value ? formatBrl(calendario.value.totalAnoBrl) : '—',
)

const mesColumns: Column<MesRow>[] = [
  { key: 'mesNome',           label: 'Mês',                 align: 'left'   },
  { key: 'totalBrl',          label: 'Total Vencimentos (BRL)', align: 'right' },
  { key: 'quantidadeParcelas', label: 'Qtd. Parcelas',      align: 'center' },
]
</script>

<template>
  <PageLayout max-width="wide">
    <template #header>
      <PageHeader title="Calendário de Vencimentos">
        <template #actions>
          <!-- Year selector -->
          <div class="calendario__year-selector">
            <Button
              variant="ghost"
              icon-left="i-carbon-chevron-left"
              :disabled="selectedYear <= MIN_YEAR || isLoading"
              aria-label="Ano anterior"
              @click="prevYear"
            />
            <span class="calendario__year-display">{{ selectedYear }}</span>
            <Button
              variant="ghost"
              icon-left="i-carbon-chevron-right"
              :disabled="selectedYear >= MAX_YEAR || isLoading"
              aria-label="Próximo ano"
              @click="nextYear"
            />
          </div>
        </template>
      </PageHeader>
    </template>

    <div class="calendario">
      <!-- Loading skeleton -->
      <template v-if="isLoading">
        <div class="calendario__skeletons">
          <Skeleton height="400px" />
        </div>
      </template>

      <!-- Error state -->
      <template v-else-if="isError">
        <Alert variant="error" title="Erro ao carregar calendário">
          Não foi possível obter os dados de vencimentos. Tente novamente.
        </Alert>
      </template>

      <!-- Empty (no meses returned) -->
      <template v-else-if="!calendario || calendario.meses.length === 0">
        <EmptyState
          icon="i-carbon-calendar"
          title="Nenhum vencimento encontrado"
          :description="`Não há vencimentos registrados para o ano ${selectedYear}.`"
        />
      </template>

      <!-- Data -->
      <template v-else>
        <Card :title="`Vencimentos ${selectedYear}`" padding="md">
          <DataTable
            :columns="mesColumns"
            :data="mesRows"
          >
            <template #cell-mesNome="{ row }">
              <div class="calendario__mes-cell">
                <span>{{ row.mesNome }}</span>
                <Badge v-if="row.isDestaque" variant="warning" size="sm" pill>
                  Alto
                </Badge>
              </div>
            </template>
          </DataTable>

          <!-- Annual total -->
          <div class="calendario__total-row">
            <span class="calendario__total-label">Total Anual</span>
            <span class="calendario__total-value">{{ totalAnoFmt }}</span>
          </div>
        </Card>
      </template>
    </div>
  </PageLayout>
</template>

<style scoped>
.calendario {
  padding: 1.5rem;
}

.calendario__skeletons {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.calendario__year-selector {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.calendario__year-display {
  font-size: 1.125rem;
  font-weight: 600;
  min-width: 3rem;
  text-align: center;
  color: var(--color-text-primary, #111827);
}

.calendario__mes-cell {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.calendario__total-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 2px solid var(--color-border, #e5e7eb);
}

.calendario__total-label {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-secondary, #374151);
}

.calendario__total-value {
  font-size: 1.125rem;
  font-weight: 700;
  color: var(--color-text-primary, #111827);
}
</style>

<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  Button,
  DataTable,
  Alert,
  Skeleton,
  Progress,
  DashboardGrid,
} from '@nordware/design-system'
import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { formatBrl } from '@/shared/money/formatMoney'
import { formatInstant } from '@/shared/dates/formatInstant'
import { useBancosOptions } from '@/features/bancos/api/useBancos'
import type { PainelGarantiasDto, LinhaDistribuicaoTipoDto, LinhaDistribuicaoBancoDto } from '@/shared/api/types'

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
// Table rows
// ============================================================================

interface TipoRow {
  tipo: string
  valorBrl: string
  percentual: number
  percentualFmt: string
}

interface BancoRow {
  bancoNome: string
  valorBrl: string
  percentual: number
  percentualFmt: string
}

// ============================================================================
// Queries
// ============================================================================

const {
  data: painel,
  isLoading,
  isError,
  refetch,
} = useQuery({
  queryKey: ['painel', 'garantias'] as const,
  queryFn: async (): Promise<PainelGarantiasDto> => {
    const { data } = await apiClient.get<PainelGarantiasDto>(API.painel.garantias)
    return data
  },
  staleTime: 60_000,
})

const { data: bancos } = useBancosOptions()

// ============================================================================
// Banco lookup map
// ============================================================================

const bancosMap = computed<Map<string, string>>(() => {
  const map = new Map<string, string>()
  for (const banco of bancos.value ?? []) {
    map.set(banco.id, banco.apelido)
  }
  return map
})

// ============================================================================
// Computed display values
// ============================================================================

const totalGarantiasFmt = computed(() =>
  painel.value ? formatBrl(painel.value.totalGarantiasAtivasBrl) : '—',
)

const dataCalculoFmt = computed(() =>
  formatInstant(painel.value?.dataCalculo),
)

// ============================================================================
// Table data
// ============================================================================

const tipoRows = computed<TipoRow[]>(() =>
  (painel.value?.distribuicaoPorTipo ?? []).map((linha: LinhaDistribuicaoTipoDto) => ({
    tipo: linha.tipo,
    valorBrl: formatBrl(linha.valorBrl),
    percentual: Math.min(linha.percentualDoTotal * 100, 100),
    percentualFmt: `${(linha.percentualDoTotal * 100).toFixed(1)}%`,
  })),
)

const bancoRows = computed<BancoRow[]>(() =>
  (painel.value?.distribuicaoPorBanco ?? []).map((linha: LinhaDistribuicaoBancoDto) => ({
    bancoNome: bancosMap.value.get(linha.bancoId) ?? linha.bancoId,
    valorBrl: formatBrl(linha.valorBrl),
    percentual: Math.min(linha.percentualDoTotal * 100, 100),
    percentualFmt: `${(linha.percentualDoTotal * 100).toFixed(1)}%`,
  })),
)

const tipoColumns: Column<TipoRow>[] = [
  { key: 'tipo',          label: 'Tipo de Garantia', align: 'left'   },
  { key: 'valorBrl',      label: 'Valor BRL',        align: 'right'  },
  { key: 'percentualFmt', label: '% do Total',       align: 'right'  },
]

const bancoColumns: Column<BancoRow>[] = [
  { key: 'bancoNome',     label: 'Banco',       align: 'left'   },
  { key: 'valorBrl',      label: 'Valor BRL',   align: 'right'  },
  { key: 'percentualFmt', label: '% do Total',  align: 'right'  },
]

// ============================================================================
// Handlers
// ============================================================================

function onRefetch(): void {
  void refetch()
}
</script>

<template>
  <PageLayout max-width="wide">
    <template #header>
      <PageHeader title="Painel de Garantias">
        <template #actions>
          <Button
            variant="ghost"
            icon-left="i-carbon-renew"
            :disabled="isLoading"
            @click="onRefetch"
          >
            Atualizar
          </Button>
        </template>
      </PageHeader>
    </template>

    <div class="painel-garantias">
      <!-- Loading skeleton -->
      <template v-if="isLoading">
        <div class="painel-garantias__skeletons">
          <Skeleton height="80px" />
          <div class="painel-garantias__grid">
            <Skeleton height="280px" />
            <Skeleton height="280px" />
          </div>
        </div>
      </template>

      <!-- Error state -->
      <template v-else-if="isError">
        <Alert variant="error" title="Erro ao carregar painel">
          Não foi possível obter os dados de garantias. Tente novamente.
        </Alert>
      </template>

      <!-- Data -->
      <template v-else-if="painel">
        <!-- Summary -->
        <Card padding="md">
          <div class="painel-garantias__summary">
            <div>
              <div class="painel-garantias__summary-label">Total de Garantias Ativas</div>
              <div class="painel-garantias__summary-value">{{ totalGarantiasFmt }}</div>
            </div>
            <div class="painel-garantias__summary-meta">
              Calculado em: {{ dataCalculoFmt }}
            </div>
          </div>
        </Card>

        <!-- Tables row -->
        <DashboardGrid :columns="2" gap="md" :responsive="true">
          <!-- Por tipo -->
          <Card title="Distribuição por Tipo" padding="md">
            <DataTable
              :columns="tipoColumns"
              :data="tipoRows"
            >
              <template #cell-percentualFmt="{ row }">
                <div class="painel-garantias__progress-cell">
                  <span class="painel-garantias__progress-label">{{ row.percentualFmt }}</span>
                  <Progress :value="row.percentual" size="sm" />
                </div>
              </template>
            </DataTable>
          </Card>

          <!-- Por banco -->
          <Card title="Distribuição por Banco" padding="md">
            <DataTable
              :columns="bancoColumns"
              :data="bancoRows"
            >
              <template #cell-percentualFmt="{ row }">
                <div class="painel-garantias__progress-cell">
                  <span class="painel-garantias__progress-label">{{ row.percentualFmt }}</span>
                  <Progress :value="row.percentual" size="sm" />
                </div>
              </template>
            </DataTable>
          </Card>
        </DashboardGrid>

        <!-- Alertas -->
        <template v-if="painel.alertas.length > 0">
          <div class="painel-garantias__alertas">
            <Alert
              v-for="(alerta, idx) in painel.alertas"
              :key="idx"
              variant="warning"
            >
              {{ alerta }}
            </Alert>
          </div>
        </template>
      </template>
    </div>
  </PageLayout>
</template>

<style scoped>
.painel-garantias {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1.5rem;
}

.painel-garantias__skeletons {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.painel-garantias__grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.painel-garantias__summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1rem;
}

.painel-garantias__summary-label {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  margin-bottom: 0.25rem;
}

.painel-garantias__summary-value {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--color-text-primary, #111827);
}

.painel-garantias__summary-meta {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.painel-garantias__progress-cell {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 120px;
}

.painel-garantias__progress-label {
  font-size: 0.875rem;
  font-weight: 500;
  text-align: right;
}

.painel-garantias__alertas {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
</style>

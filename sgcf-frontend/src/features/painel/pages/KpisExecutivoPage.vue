<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  Alert,
  Skeleton,
  Progress,
  DashboardGrid,
  EmptyState,
} from '@nordware/design-system'
import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { formatBrl } from '@/shared/money/formatMoney'
import type { KpiDto } from '@/shared/api/types'

// ============================================================================
// Query
// ============================================================================

const {
  data: kpis,
  isLoading,
  isError,
} = useQuery({
  queryKey: ['painel', 'kpis'] as const,
  queryFn: async (): Promise<KpiDto> => {
    const { data } = await apiClient.get<KpiDto>(API.painel.kpis)
    return data
  },
  staleTime: 60_000,
})

// ============================================================================
// Computed display values
// ============================================================================

const dividaTotalFmt = computed(() =>
  kpis.value ? formatBrl(kpis.value.dividaTotalBrl) : '—',
)

const custoMedioFmt = computed(() => {
  if (!kpis.value) return '—'
  return `${kpis.value.custoMedioPonderadoAa.toFixed(2).replace('.', ',')}% a.a.`
})

const prazoMedioFmt = computed(() => {
  if (!kpis.value) return '—'
  return `${kpis.value.prazoMedioMeses.toFixed(1).replace('.', ',')} meses`
})

const concentracaoMaxima = computed(() => {
  const bancos = kpis.value?.concentracaoPorBanco ?? []
  if (bancos.length === 0) return 0
  return Math.max(...bancos.map((b) => b.percentual))
})

const concentracaoMaximaFmt = computed(() => {
  const max = concentracaoMaxima.value
  return max > 0 ? `${(max * 100).toFixed(1).replace('.', ',')}%` : '—'
})

// ============================================================================
// Concentration badge variant
// ============================================================================

function concentracaoVariant(percentual: number): 'success' | 'warning' | 'danger' {
  // percentual is a fraction (0.0 to 1.0)
  if (percentual >= 0.5) return 'danger'
  if (percentual >= 0.3) return 'warning'
  return 'success'
}
</script>

<template>
  <PageLayout max-width="wide">
    <template #header>
      <PageHeader title="KPIs Executivos" />
    </template>

    <div class="kpis-executivo">
      <!-- Loading skeleton -->
      <template v-if="isLoading">
        <div class="kpis-executivo__skeletons">
          <div class="kpis-executivo__kpi-row">
            <Skeleton v-for="n in 4" :key="n" height="120px" />
          </div>
          <Skeleton height="320px" />
        </div>
      </template>

      <!-- Error state -->
      <template v-else-if="isError">
        <Alert variant="error" title="Erro ao carregar KPIs">
          Não foi possível obter os indicadores executivos. Tente novamente.
        </Alert>
      </template>

      <!-- Data -->
      <template v-else-if="kpis">
        <!-- KPI summary cards -->
        <DashboardGrid :columns="4" gap="md" :responsive="true">
          <Card title="Dívida Total" padding="md">
            <div class="kpis-executivo__kpi-value">
              {{ dividaTotalFmt }}
            </div>
            <div class="kpis-executivo__kpi-sub">Carteira total em BRL</div>
          </Card>

          <Card title="Custo Médio Ponderado" padding="md">
            <div class="kpis-executivo__kpi-value">
              {{ custoMedioFmt }}
            </div>
            <div class="kpis-executivo__kpi-sub">Taxa média da carteira</div>
          </Card>

          <Card title="Prazo Médio" padding="md">
            <div class="kpis-executivo__kpi-value">
              {{ prazoMedioFmt }}
            </div>
            <div class="kpis-executivo__kpi-sub">Duration da carteira</div>
          </Card>

          <Card title="Concentração Máxima" padding="md">
            <div
              class="kpis-executivo__kpi-value"
              :class="{
                'kpis-executivo__kpi-value--danger':  concentracaoMaxima >= 0.5,
                'kpis-executivo__kpi-value--warning': concentracaoMaxima >= 0.3 && concentracaoMaxima < 0.5,
              }"
            >
              {{ concentracaoMaximaFmt }}
            </div>
            <div class="kpis-executivo__kpi-sub">Maior banco da carteira</div>
          </Card>
        </DashboardGrid>

        <!-- Concentração por banco -->
        <Card title="Concentração por Banco" padding="md">
          <template v-if="kpis.concentracaoPorBanco.length === 0">
            <EmptyState
              icon="i-carbon-chart-bar"
              title="Sem dados de concentração"
              description="Nenhum banco encontrado na carteira atual."
            />
          </template>
          <template v-else>
            <div class="kpis-executivo__concentracao-list">
              <div
                v-for="banco in kpis.concentracaoPorBanco"
                :key="banco.bancoId"
                class="kpis-executivo__concentracao-item"
              >
                <div class="kpis-executivo__concentracao-header">
                  <span class="kpis-executivo__concentracao-nome">
                    {{ banco.apelido }}
                  </span>
                  <span
                    class="kpis-executivo__concentracao-pct"
                    :class="`kpis-executivo__concentracao-pct--${concentracaoVariant(banco.percentual)}`"
                  >
                    {{ (banco.percentual * 100).toFixed(1).replace('.', ',') }}%
                  </span>
                </div>
                <Progress
                  :value="Math.min(banco.percentual * 100, 100)"
                  size="sm"
                />
              </div>
            </div>
          </template>
        </Card>
      </template>
    </div>
  </PageLayout>
</template>

<style scoped>
.kpis-executivo {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1.5rem;
}

.kpis-executivo__skeletons {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.kpis-executivo__kpi-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1rem;
}

.kpis-executivo__kpi-value {
  font-size: 1.4rem;
  font-weight: 700;
  color: var(--color-text-primary, #111827);
  margin-bottom: 0.25rem;
}

.kpis-executivo__kpi-value--danger {
  color: var(--color-error);
}

.kpis-executivo__kpi-value--warning {
  color: var(--color-warning, #d97706);
}

.kpis-executivo__kpi-sub {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.kpis-executivo__concentracao-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.kpis-executivo__concentracao-item {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.kpis-executivo__concentracao-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.kpis-executivo__concentracao-nome {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-secondary, #374151);
}

.kpis-executivo__concentracao-pct {
  font-size: 0.875rem;
  font-weight: 600;
}

.kpis-executivo__concentracao-pct--success {
  color: var(--color-success, #16a34a);
}

.kpis-executivo__concentracao-pct--warning {
  color: var(--color-warning, #d97706);
}

.kpis-executivo__concentracao-pct--danger {
  color: var(--color-error);
}
</style>

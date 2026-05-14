<script setup lang="ts">
import { computed } from 'vue'
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
  DashboardGrid,
} from '@nordware/design-system'
import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { formatMoney, formatBrl } from '@/shared/money/formatMoney'
import { formatInstant } from '@/shared/dates/formatInstant'
import type { PainelDividaDto, LinhaBreakdownMoedaDto } from '@/shared/api/types'
import type { MoedaCode } from '@/shared/money/Money'

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

interface BreakdownRow {
  moeda: string
  saldoOriginal: string
  cotacao: string
  saldoBrl: string
  quantidadeContratos: number
}

// ============================================================================
// Query
// ============================================================================

const {
  data: painel,
  isLoading,
  isError,
  refetch,
} = useQuery({
  queryKey: ['painel', 'divida'] as const,
  queryFn: async (): Promise<PainelDividaDto> => {
    const { data } = await apiClient.get<PainelDividaDto>(API.painel.divida)
    return data
  },
  staleTime: 60_000,
})

// ============================================================================
// Computed display values
// ============================================================================

const dividaBrutaFmt = computed(() =>
  painel.value ? formatBrl(painel.value.dividaBrutaBrl) : '—',
)

const ajusteMtmFmt = computed(() => {
  if (!painel.value) return '—'
  const val = painel.value.ajusteMtm.mtmLiquidoBrl
  return formatBrl(val)
})

const dividaLiquidaFmt = computed(() =>
  painel.value ? formatBrl(painel.value.dividaLiquidaPosHedgeBrl) : '—',
)

const ajusteMtmIsNegative = computed(() =>
  (painel.value?.ajusteMtm.mtmLiquidoBrl ?? 0) < 0,
)

const tipoCotacaoLabel = computed((): string => {
  const map: Record<string, string> = {
    PtaxD1: 'PTAX D-1',
    SpotIntraday: 'Spot Intraday',
    Manual: 'Manual',
  }
  return map[painel.value?.tipoCotacao ?? ''] ?? (painel.value?.tipoCotacao ?? '')
})

const dataHoraCalculo = computed(() =>
  formatInstant(painel.value?.dataHoraCalculo),
)

// ============================================================================
// Table
// ============================================================================

const breakdownRows = computed<BreakdownRow[]>(() =>
  (painel.value?.breakdownPorMoeda ?? []).map((linha: LinhaBreakdownMoedaDto) => ({
    moeda: linha.moeda,
    saldoOriginal: formatMoney(linha.saldoMoedaOriginal, linha.moeda as MoedaCode),
    cotacao: linha.moeda === 'Brl'
      ? '—'
      : formatMoney(linha.cotacaoAplicada, 'Brl', { decimals: 4 }),
    saldoBrl: formatBrl(linha.saldoBrl),
    quantidadeContratos: linha.quantidadeContratos,
  })),
)

const breakdownColumns: Column<BreakdownRow>[] = [
  { key: 'moeda',               label: 'Moeda',               align: 'left'  },
  { key: 'saldoOriginal',       label: 'Saldo (Moeda Original)', align: 'right' },
  { key: 'cotacao',             label: 'Cotação (BRL)',        align: 'right' },
  { key: 'saldoBrl',            label: 'Saldo BRL',            align: 'right' },
  { key: 'quantidadeContratos', label: 'Contratos',            align: 'center' },
]

// ============================================================================
// Helpers
// ============================================================================

function onRefetch(): void {
  void refetch()
}

</script>

<template>
  <PageLayout max-width="wide">
    <template #header>
      <PageHeader title="Painel de Dívida">
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

    <div class="painel-divida">
      <!-- Loading skeleton -->
      <template v-if="isLoading">
        <div class="painel-divida__skeletons">
          <div class="painel-divida__kpi-row">
            <Skeleton v-for="n in 3" :key="n" height="120px" />
          </div>
          <Skeleton height="240px" />
        </div>
      </template>

      <!-- Error state -->
      <template v-else-if="isError">
        <Alert variant="error" title="Erro ao carregar painel">
          Não foi possível obter os dados de dívida. Tente novamente.
        </Alert>
      </template>

      <!-- Data -->
      <template v-else-if="painel">
        <!-- Metadata row -->
        <div class="painel-divida__meta">
          <span class="painel-divida__meta-label">
            Calculado em: {{ dataHoraCalculo }}
          </span>
          <Badge variant="info" size="sm">
            {{ tipoCotacaoLabel }}
          </Badge>
        </div>

        <!-- KPI cards -->
        <DashboardGrid :columns="3" gap="md" :responsive="true">
          <Card title="Dívida Bruta" padding="md">
            <div class="painel-divida__kpi-value">
              {{ dividaBrutaFmt }}
            </div>
            <div class="painel-divida__kpi-sub">Total em BRL</div>
          </Card>

          <Card title="Ajuste MTM Líquido" padding="md">
            <div
              class="painel-divida__kpi-value"
              :class="{ 'painel-divida__kpi-value--negative': ajusteMtmIsNegative }"
            >
              {{ ajusteMtmFmt }}
            </div>
            <div class="painel-divida__kpi-sub">
              A receber: {{ formatBrl(painel.ajusteMtm.mtmAReceberBrl) }}
              &nbsp;/&nbsp;
              A pagar: {{ formatBrl(painel.ajusteMtm.mtmAPagarBrl) }}
            </div>
          </Card>

          <Card title="Dívida Líquida Pós-Hedge" padding="md">
            <div class="painel-divida__kpi-value painel-divida__kpi-value--highlight">
              {{ dividaLiquidaFmt }}
            </div>
            <div class="painel-divida__kpi-sub">Exposição líquida em BRL</div>
          </Card>
        </DashboardGrid>

        <!-- Breakdown por moeda -->
        <Card title="Breakdown por Moeda" padding="md">
          <DataTable
            :columns="breakdownColumns"
            :data="breakdownRows"
          />
        </Card>

        <!-- Alertas -->
        <template v-if="painel.alertas.length > 0">
          <div class="painel-divida__alertas">
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
.painel-divida {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1.5rem;
}

.painel-divida__skeletons {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.painel-divida__kpi-row {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1rem;
}

.painel-divida__meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.painel-divida__meta-label {
  font-size: 0.875rem;
  color: var(--color-text-secondary, #6b7280);
}

.painel-divida__kpi-value {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--color-text-primary, #111827);
  margin-bottom: 0.25rem;
}

.painel-divida__kpi-value--negative {
  color: var(--color-error, #dc2626);
}

.painel-divida__kpi-value--highlight {
  color: var(--color-primary, #1d4ed8);
}

.painel-divida__kpi-sub {
  font-size: 0.75rem;
  color: var(--color-text-secondary, #6b7280);
}

.painel-divida__alertas {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
</style>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  Badge,
  Button,
  Alert,
  Skeleton,
  Spinner,
} from '@nordware/design-system'
import RoleGate from '@/shared/auth/RoleGate.vue'
import { apiClient, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { formatBrl } from '@/shared/money/formatMoney'
import type { BancoDto } from '@/shared/api/types'

type BadgeVariant = 'default' | 'primary' | 'info' | 'success' | 'warning' | 'danger'

// ============================================================================
// Route
// ============================================================================

const route = useRoute()
const router = useRouter()

const id = computed(() => route.params['id'] as string)

// ============================================================================
// Query
// ============================================================================

const {
  data: banco,
  isLoading,
  isError,
  error: queryError,
} = useQuery({
  queryKey: computed(() => ['bancos', id.value]),
  queryFn: async (): Promise<BancoDto> => {
    const { data } = await apiClient.get<BancoDto>(API.bancos.get(id.value))
    return data
  },
  enabled: computed(() => !!id.value),
})

// ============================================================================
// Helpers
// ============================================================================

const PADRAO_LABELS: Record<string, string> = {
  LiquidacaoTotal:   'Liquidação Total',
  LiquidacaoParcial: 'Liquidação Parcial',
  AmbosOsModelos:    'Ambos os Modelos',
}

function padraoLabel(val: string): string {
  return PADRAO_LABELS[val] ?? val
}

function boolLabel(val: boolean): string {
  return val ? 'Sim' : 'Não'
}

function boolBadge(val: boolean): BadgeVariant {
  return val ? 'success' : 'default'
}

function pctLabel(val: number | null): string {
  return val !== null ? `${val}%` : '—'
}

const pageTitle = computed(() => banco.value?.apelido ?? 'Banco')
</script>

<template>
  <PageLayout max-width="full">
    <template #header>
      <PageHeader :title="pageTitle">
        <template #actions>
          <Button
            variant="ghost"
            size="sm"
            icon-left="i-carbon-arrow-left"
            @click="() => void router.push('/bancos')"
          >
            Bancos
          </Button>

          <RoleGate v-if="banco" policy="Admin">
            <Button
              variant="secondary"
              size="md"
              icon-left="i-carbon-settings"
              @click="() => void router.push(`/bancos/${id}/editar-config`)"
            >
              Editar Config.
            </Button>
          </RoleGate>
        </template>
      </PageHeader>
    </template>

    <!-- Loading -->
    <div v-if="isLoading" class="detail-loading">
      <Spinner size="lg" />
      <div class="detail-skeletons">
        <Skeleton height="40px" />
        <Skeleton height="220px" />
        <Skeleton height="260px" />
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="isError" class="detail-error">
      <Alert variant="error" title="Erro ao carregar banco">
        {{ extractApiError(queryError) }}
      </Alert>
    </div>

    <!-- Content -->
    <template v-else-if="banco">
      <div class="detail-content">
        <!-- Two-column layout -->
        <div class="detail-grid">
          <!-- Left column: Dados Básicos -->
          <Card>
            <h3 class="card-section-title">Dados Básicos</h3>
            <div class="info-grid">
              <div class="info-field">
                <span class="info-field__label">Código COMPE</span>
                <span class="info-field__value info-field__value--mono">{{ banco.codigoCompe }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">Razão Social</span>
                <span class="info-field__value">{{ banco.razaoSocial }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">Apelido</span>
                <span class="info-field__value">{{ banco.apelido }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">Padrão Antecipação</span>
                <span class="info-field__value">{{ padraoLabel(banco.padraoAntecipacao) }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">Limite Crédito BRL</span>
                <span class="info-field__value">
                  {{ banco.limiteCreditoBrl !== null ? formatBrl(banco.limiteCreditoBrl) : '—' }}
                </span>
              </div>
            </div>
          </Card>

          <!-- Right column: Config Antecipação -->
          <Card>
            <h3 class="card-section-title">Config. Antecipação</h3>
            <div class="info-grid">
              <div class="info-field">
                <span class="info-field__label">Aceita Liquidação Total</span>
                <Badge :variant="boolBadge(banco.aceitaLiquidacaoTotal)" size="sm">
                  {{ boolLabel(banco.aceitaLiquidacaoTotal) }}
                </Badge>
              </div>
              <div class="info-field">
                <span class="info-field__label">Aceita Liquidação Parcial</span>
                <Badge :variant="boolBadge(banco.aceitaLiquidacaoParcial)" size="sm">
                  {{ boolLabel(banco.aceitaLiquidacaoParcial) }}
                </Badge>
              </div>
              <div class="info-field">
                <span class="info-field__label">Exige Anuência Expressa</span>
                <Badge :variant="boolBadge(banco.exigeAnuenciaExpressa)" size="sm">
                  {{ boolLabel(banco.exigeAnuenciaExpressa) }}
                </Badge>
              </div>
              <div class="info-field">
                <span class="info-field__label">Exige Parcela Inteira</span>
                <Badge :variant="boolBadge(banco.exigeParcelaInteira)" size="sm">
                  {{ boolLabel(banco.exigeParcelaInteira) }}
                </Badge>
              </div>
              <div class="info-field">
                <span class="info-field__label">Aviso Prévio Min. (dias úteis)</span>
                <span class="info-field__value">{{ banco.avisoPrevioMinDiasUteis }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">Valor Mínimo Parcial (%)</span>
                <span class="info-field__value">{{ pctLabel(banco.valorMinimoParcialPct) }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">Break Funding Fee (%)</span>
                <span class="info-field__value">{{ pctLabel(banco.breakFundingFeePct) }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">TLA % sobre Saldo</span>
                <span class="info-field__value">{{ pctLabel(banco.tlaPctSobreSaldo) }}</span>
              </div>
              <div class="info-field">
                <span class="info-field__label">TLA % por Mês Remanescente</span>
                <span class="info-field__value">{{ pctLabel(banco.tlaPctPorMesRemanescente) }}</span>
              </div>
              <div v-if="banco.observacoesAntecipacao" class="info-field info-field--full">
                <span class="info-field__label">Observações Antecipação</span>
                <span class="info-field__value">{{ banco.observacoesAntecipacao }}</span>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </template>
  </PageLayout>
</template>

<style scoped>
.detail-loading {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 2rem;
  align-items: center;
}

.detail-skeletons {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  width: 100%;
  max-width: 900px;
}

.detail-error {
  padding: 2rem;
}

.detail-content {
  padding: 1.5rem;
}

.detail-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
}

@media (max-width: 900px) {
  .detail-grid {
    grid-template-columns: 1fr;
  }
}

.card-section-title {
  font-size: 0.875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  margin: 0 0 1rem;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1.25rem;
}

.info-field {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.info-field--full {
  grid-column: 1 / -1;
}

.info-field__label {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.info-field__value {
  font-size: 0.9375rem;
  color: var(--color-text-primary);
}

.info-field__value--mono {
  font-family: ui-monospace, 'Cascadia Code', 'Source Code Pro', monospace;
  font-size: 0.875rem;
}
</style>

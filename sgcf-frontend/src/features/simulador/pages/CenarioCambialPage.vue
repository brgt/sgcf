<script setup lang="ts">
import { ref, reactive } from 'vue'
import {
  Button,
  Input,
  Card,
  Alert,
} from '@nordware/design-system'
import { postIdempotent, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { toast } from '@/shared/ui/toast'

// ============================================================================
// Types mirroring the backend DTOs
// ============================================================================

interface LinhaCenarioMoeda {
  moeda: string
  cotacaoBase: number
  cotacaoEstressada: number
  deltaPct: number
  saldoBrlBase: number
  saldoBrlEstressado: number
  impactoBrl: number
}

interface Cenario {
  nome: string
  breakdownPorMoeda: LinhaCenarioMoeda[]
  dividaBrutaBrl: number
  dividaLiquidaBrl: number
  deltaVsRealistaBrl: number
}

interface ResultadoCenarioCambial {
  cenarioCustomizado: Cenario
  cenarioPessimista: Cenario
  cenarioRealista: Cenario
  cenarioOtimista: Cenario
}

// ============================================================================
// State
// ============================================================================

const isSimulating = ref(false)
const resultado = ref<ResultadoCenarioCambial | null>(null)

// All deltas are optional — null means "not set" (backend treats as 0)
const form = reactive({
  deltaUsdPct: null as number | null,
  deltaEurPct: null as number | null,
  deltaJpyPct: null as number | null,
  deltaCnyPct: null as number | null,
})

// ============================================================================
// Helpers
// ============================================================================

const BRL = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 2,
})

const PCT = new Intl.NumberFormat('pt-BR', {
  style: 'percent',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

function formatBrl(value: number): string {
  return BRL.format(value)
}

function formatPct(value: number): string {
  // value comes as e.g. 10.5 meaning 10.5%, divide by 100 for Intl
  return PCT.format(value / 100)
}

function formatDelta(value: number): string {
  const sign = value >= 0 ? '+' : ''
  return `${sign}${formatBrl(value)}`
}

// ============================================================================
// Simulate
// ============================================================================

async function simulate() {
  isSimulating.value = true
  resultado.value = null
  try {
    const body = {
      deltaUsdPct: form.deltaUsdPct,
      deltaEurPct: form.deltaEurPct,
      deltaJpyPct: form.deltaJpyPct,
      deltaCnyPct: form.deltaCnyPct,
    }
    resultado.value = await postIdempotent<ResultadoCenarioCambial>(
      API.simulador.cenarioCambial,
      body,
    )
  } catch (err: unknown) {
    toast.error(extractApiError(err))
  } finally {
    isSimulating.value = false
  }
}

function limpar() {
  form.deltaUsdPct = null
  form.deltaEurPct = null
  form.deltaJpyPct = null
  form.deltaCnyPct = null
  resultado.value = null
}
</script>

<template>
  <div class="cenario-cambial-page">
    <div class="page-header">
      <h1 class="page-title">Simulador — Cenário Cambial</h1>
      <p class="page-subtitle">
        Simule o impacto de variações cambiais sobre a dívida total. Deixe os campos
        em branco para usar variação zero (cenário realista).
      </p>
    </div>

    <!-- Parameters card -->
    <Card title="Parâmetros de Simulação">
      <form class="sim-form" novalidate @submit.prevent="simulate">
        <div class="form-grid">
          <div class="field">
            <Input
              :model-value="form.deltaUsdPct ?? ''"
              label="Delta USD (%)"
              type="number"
              placeholder="Ex: 10 ou -5"
              full-width
              @update:model-value="(v) => (form.deltaUsdPct = v === '' || v === null ? null : Number(v))"
            />
          </div>

          <div class="field">
            <Input
              :model-value="form.deltaEurPct ?? ''"
              label="Delta EUR (%)"
              type="number"
              placeholder="Ex: 10 ou -5"
              full-width
              @update:model-value="(v) => (form.deltaEurPct = v === '' || v === null ? null : Number(v))"
            />
          </div>

          <div class="field">
            <Input
              :model-value="form.deltaJpyPct ?? ''"
              label="Delta JPY (%)"
              type="number"
              placeholder="Ex: 10 ou -5"
              full-width
              @update:model-value="(v) => (form.deltaJpyPct = v === '' || v === null ? null : Number(v))"
            />
          </div>

          <div class="field">
            <Input
              :model-value="form.deltaCnyPct ?? ''"
              label="Delta CNY (%)"
              type="number"
              placeholder="Ex: 10 ou -5"
              full-width
              @update:model-value="(v) => (form.deltaCnyPct = v === '' || v === null ? null : Number(v))"
            />
          </div>
        </div>

        <div class="form-actions">
          <Button
            type="button"
            variant="ghost"
            :disabled="isSimulating"
            @click="limpar"
          >
            Limpar
          </Button>
          <Button
            type="submit"
            variant="primary"
            :loading="isSimulating"
            :disabled="isSimulating"
          >
            Simular
          </Button>
        </div>
      </form>
    </Card>

    <!-- Results -->
    <template v-if="resultado">
      <div class="results-header">
        <h2 class="results-title">Resultados da Simulação</h2>
      </div>

      <!-- Cenário summary row -->
      <div class="cenarios-grid">
        <div
          v-for="cenario in [
            resultado.cenarioPessimista,
            resultado.cenarioRealista,
            resultado.cenarioOtimista,
            resultado.cenarioCustomizado,
          ]"
          :key="cenario.nome"
          class="cenario-card"
          :class="{
            'cenario-card--pessimista': cenario.nome.toLowerCase().includes('pessimista'),
            'cenario-card--realista': cenario.nome.toLowerCase().includes('realista'),
            'cenario-card--otimista': cenario.nome.toLowerCase().includes('otimista'),
            'cenario-card--customizado': cenario.nome.toLowerCase().includes('customizado'),
          }"
        >
          <p class="cenario-card__nome">{{ cenario.nome }}</p>
          <p class="cenario-card__divida-bruta">{{ formatBrl(cenario.dividaBrutaBrl) }}</p>
          <p class="cenario-card__label">Dívida Bruta BRL</p>
          <p class="cenario-card__divida-liq">{{ formatBrl(cenario.dividaLiquidaBrl) }}</p>
          <p class="cenario-card__label">Dívida Líquida BRL</p>
          <p
            class="cenario-card__delta"
            :class="{
              'delta--positive': cenario.deltaVsRealistaBrl > 0,
              'delta--negative': cenario.deltaVsRealistaBrl < 0,
              'delta--neutral': cenario.deltaVsRealistaBrl === 0,
            }"
          >
            {{ formatDelta(cenario.deltaVsRealistaBrl) }}
          </p>
          <p class="cenario-card__label">vs. Realista</p>
        </div>
      </div>

      <!-- Breakdown por moeda for each cenário -->
      <div
        v-for="cenario in [
          resultado.cenarioCustomizado,
          resultado.cenarioPessimista,
          resultado.cenarioRealista,
          resultado.cenarioOtimista,
        ]"
        :key="`breakdown-${cenario.nome}`"
        class="breakdown-section"
      >
        <Card :title="`${cenario.nome} — Breakdown por Moeda`">
          <div v-if="cenario.breakdownPorMoeda.length === 0">
            <Alert variant="info">Sem exposição em moeda estrangeira.</Alert>
          </div>
          <div v-else class="table-wrapper">
            <table class="breakdown-table" aria-label="`Breakdown cambial — ${cenario.nome}`">
              <thead>
                <tr>
                  <th scope="col">Moeda</th>
                  <th scope="col" class="num">Cotação Base</th>
                  <th scope="col" class="num">Cotação Estressada</th>
                  <th scope="col" class="num">Delta %</th>
                  <th scope="col" class="num">Saldo BRL Base</th>
                  <th scope="col" class="num">Saldo BRL Estressado</th>
                  <th scope="col" class="num">Impacto BRL</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="linha in cenario.breakdownPorMoeda" :key="linha.moeda">
                  <td>
                    <span class="moeda-badge">{{ linha.moeda }}</span>
                  </td>
                  <td class="num">{{ linha.cotacaoBase.toFixed(4) }}</td>
                  <td class="num">{{ linha.cotacaoEstressada.toFixed(4) }}</td>
                  <td class="num">{{ formatPct(linha.deltaPct) }}</td>
                  <td class="num">{{ formatBrl(linha.saldoBrlBase) }}</td>
                  <td class="num">{{ formatBrl(linha.saldoBrlEstressado) }}</td>
                  <td
                    class="num"
                    :class="{
                      'impact--positive': linha.impactoBrl > 0,
                      'impact--negative': linha.impactoBrl < 0,
                    }"
                  >
                    {{ formatDelta(linha.impactoBrl) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </Card>
      </div>
    </template>
  </div>
</template>

<style scoped>
.cenario-cambial-page {
  max-width: 1100px;
  margin: 0 auto;
  padding: 2rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.page-header {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.page-title {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.page-subtitle {
  font-size: 0.9375rem;
  color: var(--color-text-secondary);
  margin: 0;
}

/* Form */
.sim-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1.25rem;
}

.field {
  min-width: 0;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding-top: 0.25rem;
}

/* Results header */
.results-header {
  border-top: 1px solid var(--color-border);
  padding-top: 0.5rem;
}

.results-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

/* Cenário summary cards */
.cenarios-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1rem;
}

.cenario-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.cenario-card--pessimista {
  border-left: 3px solid var(--color-error, #ef4444);
}

.cenario-card--realista {
  border-left: 3px solid var(--color-warning, #f59e0b);
}

.cenario-card--otimista {
  border-left: 3px solid var(--color-success, #10b981);
}

.cenario-card--customizado {
  border-left: 3px solid var(--color-primary, #00f9b8);
}

.cenario-card__nome {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  margin: 0 0 0.5rem;
}

.cenario-card__divida-bruta,
.cenario-card__divida-liq {
  font-size: 1rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0.125rem 0 0;
}

.cenario-card__label {
  font-size: 0.75rem;
  color: var(--color-text-tertiary);
  margin: 0 0 0.5rem;
}

.cenario-card__delta {
  font-size: 0.9375rem;
  font-weight: 600;
  margin: 0.25rem 0 0;
}

.delta--positive {
  color: var(--color-error, #ef4444);
}

.delta--negative {
  color: var(--color-success, #10b981);
}

.delta--neutral {
  color: var(--color-text-secondary);
}

/* Breakdown table */
.breakdown-section {
  /* space provided by parent gap */
}

.table-wrapper {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.breakdown-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.breakdown-table th,
.breakdown-table td {
  padding: 0.625rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
  white-space: nowrap;
}

.breakdown-table th {
  font-weight: 600;
  color: var(--color-text-secondary);
  background: var(--color-surface-elevated);
}

.breakdown-table tbody tr:last-child td {
  border-bottom: none;
}

.breakdown-table .num {
  text-align: right;
}

.moeda-badge {
  display: inline-block;
  padding: 0.125rem 0.5rem;
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  border-radius: var(--radius-sm);
  font-size: 0.8125rem;
  font-weight: 600;
}

.impact--positive {
  color: var(--color-error, #ef4444);
  font-weight: 600;
}

.impact--negative {
  color: var(--color-success, #10b981);
  font-weight: 600;
}

@media (max-width: 900px) {
  .cenarios-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .form-grid {
    grid-template-columns: 1fr;
  }

  .cenarios-grid {
    grid-template-columns: 1fr;
  }
}
</style>

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

interface RecomendacaoAntecipacao {
  contratoId: string
  numeroExterno: string
  banco: string
  modalidade: string
  economiaLiquidaBrl: number
  custoPrepagamentoBrl: number
  valorTotalAntecipacaoBrl: number
  justificativaOtimizacao: string
  restricoes: string[]
}

interface ResultadoAntecipacaoPortfolio {
  caixaDisponivelBrl: number
  rankingTop5: RecomendacaoAntecipacao[]
  contratosExcluidos: string[]
}

// ============================================================================
// State
// ============================================================================

const isSimulating = ref(false)
const resultado = ref<ResultadoAntecipacaoPortfolio | null>(null)

const form = reactive({
  caixaDisponivelBrl: null as number | null,
  taxaCdiAa: null as number | null,
})

// ============================================================================
// Helpers
// ============================================================================

const BRL = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 2,
})

function formatBrl(value: number): string {
  return BRL.format(value)
}

// ============================================================================
// Simulate
// ============================================================================

async function simulate() {
  if (!form.caixaDisponivelBrl || form.caixaDisponivelBrl <= 0) {
    toast.error('Informe um valor de caixa disponível maior que zero.')
    return
  }

  isSimulating.value = true
  resultado.value = null
  try {
    resultado.value = await postIdempotent<ResultadoAntecipacaoPortfolio>(
      API.simulador.antecipacaoPortfolio,
      {
        caixaDisponivelBrl: form.caixaDisponivelBrl,
        taxaCdiAa: form.taxaCdiAa,
      },
    )
  } catch (err: unknown) {
    toast.error(extractApiError(err))
  } finally {
    isSimulating.value = false
  }
}

function limpar() {
  form.caixaDisponivelBrl = null
  form.taxaCdiAa = null
  resultado.value = null
}
</script>

<template>
  <div class="antecipacao-page">
    <div class="page-header">
      <h1 class="page-title">Simulador — Antecipação de Portfólio</h1>
      <p class="page-subtitle">
        Ranqueia os top-5 contratos com maior economia líquida de antecipação,
        considerando o custo de oportunidade do CDI.
      </p>
    </div>

    <!-- Parameters card -->
    <Card title="Parâmetros de Simulação">
      <form class="sim-form" novalidate @submit.prevent="simulate">
        <div class="form-grid">
          <!-- Caixa disponível (required) -->
          <div class="field">
            <Input
              :model-value="form.caixaDisponivelBrl ?? ''"
              label="Caixa Disponível (BRL)"
              type="number"
              placeholder="Ex: 5000000"
              full-width
              @update:model-value="(v) => (form.caixaDisponivelBrl = v === '' || v === null ? null : Number(v))"
            />
            <p class="field-hint">Obrigatório. Deve ser maior que zero.</p>
          </div>

          <!-- Taxa CDI (optional) -->
          <div class="field">
            <Input
              :model-value="form.taxaCdiAa ?? ''"
              label="Taxa CDI a.a. % (opcional)"
              type="number"
              placeholder="Ex: 10.75"
              full-width
              @update:model-value="(v) => (form.taxaCdiAa = v === '' || v === null ? null : Number(v))"
            />
            <p class="field-hint">Se omitido, o backend usa o CDI vigente.</p>
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
        <p class="results-subtitle">
          Caixa utilizado na simulação:
          <strong>{{ formatBrl(resultado.caixaDisponivelBrl) }}</strong>
        </p>
      </div>

      <!-- Ranking top-5 -->
      <Card title="Ranking Top-5 — Maior Economia Líquida">
        <div v-if="resultado.rankingTop5.length === 0">
          <Alert variant="info">
            Nenhum contrato elegível para antecipação com os parâmetros informados.
          </Alert>
        </div>
        <div v-else class="table-wrapper">
          <table class="ranking-table" aria-label="Ranking de antecipação de portfólio">
            <thead>
              <tr>
                <th scope="col" class="col-pos">#</th>
                <th scope="col">Contrato</th>
                <th scope="col">Banco</th>
                <th scope="col">Modalidade</th>
                <th scope="col" class="num">Economia Líq. BRL</th>
                <th scope="col" class="num">Custo Pré-pagamento</th>
                <th scope="col" class="num">Total Antecipação</th>
                <th scope="col">Justificativa</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(rec, idx) in resultado.rankingTop5"
                :key="rec.contratoId"
              >
                <td class="col-pos">
                  <span class="rank-badge" :class="`rank-badge--${idx + 1}`">
                    {{ idx + 1 }}
                  </span>
                </td>
                <td>
                  <span class="contrato-numero">{{ rec.numeroExterno }}</span>
                </td>
                <td>{{ rec.banco }}</td>
                <td>
                  <span class="modalidade-badge">{{ rec.modalidade }}</span>
                </td>
                <td class="num economia">{{ formatBrl(rec.economiaLiquidaBrl) }}</td>
                <td class="num">{{ formatBrl(rec.custoPrepagamentoBrl) }}</td>
                <td class="num">{{ formatBrl(rec.valorTotalAntecipacaoBrl) }}</td>
                <td class="justificativa">{{ rec.justificativaOtimizacao }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- Restricoes por contrato -->
        <template
          v-for="rec in resultado.rankingTop5.filter((r) => r.restricoes.length > 0)"
          :key="`restricoes-${rec.contratoId}`"
        >
          <div class="restricoes-block">
            <p class="restricoes-title">
              Restrições — {{ rec.numeroExterno }}
            </p>
            <ul class="restricoes-list">
              <li v-for="(r, i) in rec.restricoes" :key="i">{{ r }}</li>
            </ul>
          </div>
        </template>
      </Card>

      <!-- Contratos excluídos -->
      <Card
        v-if="resultado.contratosExcluidos.length > 0"
        title="Contratos Excluídos da Simulação"
      >
        <Alert variant="warning" class="excluidos-alert">
          Os contratos abaixo foram excluídos por restrições operacionais ou de banco.
        </Alert>
        <ul class="excluidos-list">
          <li v-for="(c, i) in resultado.contratosExcluidos" :key="i" class="excluido-item">
            {{ c }}
          </li>
        </ul>
      </Card>
    </template>
  </div>
</template>

<style scoped>
.antecipacao-page {
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
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.field-hint {
  font-size: 0.8125rem;
  color: var(--color-text-tertiary);
  margin: 0;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding-top: 0.25rem;
}

/* Results */
.results-header {
  border-top: 1px solid var(--color-border);
  padding-top: 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.results-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.results-subtitle {
  font-size: 0.9375rem;
  color: var(--color-text-secondary);
  margin: 0;
}

/* Table */
.table-wrapper {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.ranking-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.ranking-table th,
.ranking-table td {
  padding: 0.625rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--color-border);
  vertical-align: top;
}

.ranking-table th {
  font-weight: 600;
  color: var(--color-text-secondary);
  background: var(--color-surface-elevated);
  white-space: nowrap;
}

.ranking-table tbody tr:last-child td {
  border-bottom: none;
}

.ranking-table .num {
  text-align: right;
  white-space: nowrap;
}

.col-pos {
  width: 2.5rem;
  text-align: center !important;
}

/* Rank badge */
.rank-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.75rem;
  height: 1.75rem;
  border-radius: 50%;
  font-size: 0.8125rem;
  font-weight: 700;
  background: var(--color-surface-elevated);
  color: var(--color-text-secondary);
}

.rank-badge--1 {
  background: #fbbf24;
  color: #78350f;
}

.rank-badge--2 {
  background: #9ca3af;
  color: #1f2937;
}

.rank-badge--3 {
  background: #cd7c2f;
  color: #fff;
}

/* Content cells */
.contrato-numero {
  font-family: var(--font-family-mono, monospace);
  font-size: 0.8125rem;
}

.modalidade-badge {
  display: inline-block;
  padding: 0.125rem 0.5rem;
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  border-radius: var(--radius-sm);
  font-size: 0.8125rem;
  font-weight: 600;
  white-space: nowrap;
}

.economia {
  color: var(--color-success, #10b981);
  font-weight: 700;
}

.justificativa {
  max-width: 260px;
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  line-height: 1.4;
}

/* Restricoes */
.restricoes-block {
  margin-top: 1rem;
  padding: 0.75rem 1rem;
  background: color-mix(in srgb, var(--color-warning, #f59e0b) 8%, transparent);
  border-left: 3px solid var(--color-warning, #f59e0b);
  border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
}

.restricoes-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 0.375rem;
}

.restricoes-list {
  margin: 0;
  padding-left: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.restricoes-list li {
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

/* Excluidos */
.excluidos-alert {
  margin-bottom: 0.75rem;
}

.excluidos-list {
  margin: 0;
  padding-left: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.excluido-item {
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  font-family: var(--font-family-mono, monospace);
}

@media (max-width: 560px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>

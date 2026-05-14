<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  Button,
  Alert,
  Skeleton,
  Input,
  Select,
  Checkbox,
  Textarea,
} from '@nordware/design-system'
import { apiClient, postIdempotent, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { toast } from '@/shared/ui/toast'
import { PadraoAntecipacao } from '@/shared/api/enums'
import type { BancoDto, CreateBancoCommand, UpdateBancoConfigRequest } from '@/shared/api/types'

interface SelectOption {
  label: string
  value: string
}

// ============================================================================
// Route / mode detection
// ============================================================================

const route = useRoute()
const router = useRouter()
const queryClient = useQueryClient()

const isEditMode = computed(() => !!route.params['id'])
const id = computed(() => route.params['id'] as string | undefined)

const pageTitle = computed(() =>
  isEditMode.value ? 'Editar Config. Antecipação' : 'Novo Banco',
)

// ============================================================================
// Enum options
// ============================================================================

const padraoOptions: SelectOption[] = [
  { label: 'Liquidação Total',   value: PadraoAntecipacao.LiquidacaoTotal   },
  { label: 'Liquidação Parcial', value: PadraoAntecipacao.LiquidacaoParcial },
  { label: 'Ambos os Modelos',   value: PadraoAntecipacao.AmbosOsModelos    },
]

// ============================================================================
// Create form state
// ============================================================================

interface CreateForm {
  codigoCompe: string
  razaoSocial: string
  apelido: string
  padraoAntecipacao: string
}

const createForm = ref<CreateForm>({
  codigoCompe: '',
  razaoSocial: '',
  apelido: '',
  padraoAntecipacao: PadraoAntecipacao.LiquidacaoTotal,
})

const createErrors = ref<Partial<Record<keyof CreateForm, string>>>({})

function validateCreate(): boolean {
  const errs: Partial<Record<keyof CreateForm, string>> = {}
  if (!createForm.value.codigoCompe.trim()) errs.codigoCompe = 'Obrigatório'
  if (!createForm.value.razaoSocial.trim()) errs.razaoSocial = 'Obrigatório'
  if (!createForm.value.apelido.trim()) errs.apelido = 'Obrigatório'
  if (!createForm.value.padraoAntecipacao) errs.padraoAntecipacao = 'Obrigatório'
  createErrors.value = errs
  return Object.keys(errs).length === 0
}

// ============================================================================
// Edit form state
// ============================================================================

interface EditForm {
  aceitaLiquidacaoTotal: boolean
  aceitaLiquidacaoParcial: boolean
  exigeAnuenciaExpressa: boolean
  exigeParcelaInteira: boolean
  avisoPrevioMinDiasUteis: string
  padraoAntecipacao: string
  valorMinimoParcialPct: string
  breakFundingFeePct: string
  tlaPctSobreSaldo: string
  tlaPctPorMesRemanescente: string
  observacoesAntecipacao: string
}

const editForm = ref<EditForm>({
  aceitaLiquidacaoTotal: false,
  aceitaLiquidacaoParcial: false,
  exigeAnuenciaExpressa: false,
  exigeParcelaInteira: false,
  avisoPrevioMinDiasUteis: '0',
  padraoAntecipacao: PadraoAntecipacao.LiquidacaoTotal,
  valorMinimoParcialPct: '',
  breakFundingFeePct: '',
  tlaPctSobreSaldo: '',
  tlaPctPorMesRemanescente: '',
  observacoesAntecipacao: '',
})

// ============================================================================
// Pre-fetch existing banco for edit mode
// ============================================================================

const {
  data: existingBanco,
  isLoading: isFetchingBanco,
  isError: isFetchError,
  error: fetchError,
} = useQuery({
  queryKey: computed(() => ['bancos', id.value]),
  queryFn: async (): Promise<BancoDto> => {
    const { data } = await apiClient.get<BancoDto>(API.bancos.get(id.value!))
    return data
  },
  enabled: computed(() => isEditMode.value && !!id.value),
  staleTime: 30_000,
})

// Populate edit form when data arrives
watch(existingBanco, (banco) => {
  if (!banco) return
  editForm.value = {
    aceitaLiquidacaoTotal: banco.aceitaLiquidacaoTotal,
    aceitaLiquidacaoParcial: banco.aceitaLiquidacaoParcial,
    exigeAnuenciaExpressa: banco.exigeAnuenciaExpressa,
    exigeParcelaInteira: banco.exigeParcelaInteira,
    avisoPrevioMinDiasUteis: String(banco.avisoPrevioMinDiasUteis),
    padraoAntecipacao: banco.padraoAntecipacao,
    valorMinimoParcialPct: banco.valorMinimoParcialPct !== null ? String(banco.valorMinimoParcialPct) : '',
    breakFundingFeePct: banco.breakFundingFeePct !== null ? String(banco.breakFundingFeePct) : '',
    tlaPctSobreSaldo: banco.tlaPctSobreSaldo !== null ? String(banco.tlaPctSobreSaldo) : '',
    tlaPctPorMesRemanescente: banco.tlaPctPorMesRemanescente !== null ? String(banco.tlaPctPorMesRemanescente) : '',
    observacoesAntecipacao: banco.observacoesAntecipacao ?? '',
  }
}, { immediate: true })

// ============================================================================
// Submit
// ============================================================================

const isSubmitting = ref(false)
const submitError = ref<string | null>(null)

async function handleCreateSubmit(): Promise<void> {
  if (!validateCreate()) return
  isSubmitting.value = true
  submitError.value = null
  try {
    const body: CreateBancoCommand = {
      codigoCompe: createForm.value.codigoCompe.trim(),
      razaoSocial: createForm.value.razaoSocial.trim(),
      apelido: createForm.value.apelido.trim(),
      padraoAntecipacao: createForm.value.padraoAntecipacao as CreateBancoCommand['padraoAntecipacao'],
    }
    await postIdempotent(API.bancos.create, body)
    await queryClient.invalidateQueries({ queryKey: ['bancos'] })
    toast.success('Banco criado com sucesso.')
    void router.push('/bancos')
  } catch (err) {
    submitError.value = extractApiError(err)
    toast.error(submitError.value)
  } finally {
    isSubmitting.value = false
  }
}

function parseNullableNumber(val: string): number | null {
  const trimmed = val.trim()
  if (trimmed === '') return null
  const parsed = parseFloat(trimmed)
  return isNaN(parsed) ? null : parsed
}

async function handleEditSubmit(): Promise<void> {
  isSubmitting.value = true
  submitError.value = null
  try {
    const body: UpdateBancoConfigRequest = {
      aceitaLiquidacaoTotal: editForm.value.aceitaLiquidacaoTotal,
      aceitaLiquidacaoParcial: editForm.value.aceitaLiquidacaoParcial,
      exigeAnuenciaExpressa: editForm.value.exigeAnuenciaExpressa,
      exigeParcelaInteira: editForm.value.exigeParcelaInteira,
      avisoPrevioMinDiasUteis: parseInt(editForm.value.avisoPrevioMinDiasUteis, 10) || 0,
      padraoAntecipacao: editForm.value.padraoAntecipacao as UpdateBancoConfigRequest['padraoAntecipacao'],
      valorMinimoParcialPct: parseNullableNumber(editForm.value.valorMinimoParcialPct),
      breakFundingFeePct: parseNullableNumber(editForm.value.breakFundingFeePct),
      tlaPctSobreSaldo: parseNullableNumber(editForm.value.tlaPctSobreSaldo),
      tlaPctPorMesRemanescente: parseNullableNumber(editForm.value.tlaPctPorMesRemanescente),
      observacoesAntecipacao: editForm.value.observacoesAntecipacao.trim() || null,
    }
    await apiClient.put(API.bancos.updateConfig(id.value!), body)
    await queryClient.invalidateQueries({ queryKey: ['bancos', id.value] })
    toast.success('Configuração de antecipação salva com sucesso.')
    void router.push(`/bancos/${id.value}`)
  } catch (err) {
    submitError.value = extractApiError(err)
    toast.error(submitError.value)
  } finally {
    isSubmitting.value = false
  }
}

function handleSubmit(): void {
  if (isEditMode.value) {
    void handleEditSubmit()
  } else {
    void handleCreateSubmit()
  }
}

function goBack(): void {
  if (isEditMode.value) {
    void router.push(`/bancos/${id.value}`)
  } else {
    void router.push('/bancos')
  }
}
</script>

<template>
  <PageLayout max-width="narrow">
    <template #header>
      <PageHeader :title="pageTitle">
        <template #actions>
          <Button
            variant="ghost"
            size="sm"
            icon-left="i-carbon-arrow-left"
            @click="goBack"
          >
            {{ isEditMode ? 'Voltar' : 'Bancos' }}
          </Button>
        </template>
      </PageHeader>
    </template>

    <!-- Loading (edit pre-fetch) -->
    <div v-if="isEditMode && isFetchingBanco" class="form-loading">
      <div class="form-skeletons">
        <Skeleton height="40px" />
        <Skeleton height="300px" />
      </div>
    </div>

    <!-- Fetch error -->
    <div v-else-if="isEditMode && isFetchError" class="form-error">
      <Alert variant="error" title="Erro ao carregar dados">
        {{ extractApiError(fetchError) }}
      </Alert>
    </div>

    <!-- Form -->
    <template v-else>
      <div class="form-content">
        <!-- Submit error banner -->
        <Alert v-if="submitError" variant="error" title="Erro ao salvar">
          {{ submitError }}
        </Alert>

        <!-- ================================================================
             CREATE FORM
        ================================================================ -->
        <template v-if="!isEditMode">
          <Card>
            <h3 class="card-section-title">Dados do Banco</h3>
            <form class="form-grid" @submit.prevent="handleSubmit">
              <div class="form-field--full">
                <Input
                  v-model="createForm.codigoCompe"
                  label="Código COMPE"
                  required
                  :error="createErrors.codigoCompe || ''"
                  full-width
                  placeholder="Ex.: 001"
                />
              </div>

              <div class="form-field--full">
                <Input
                  v-model="createForm.razaoSocial"
                  label="Razão Social"
                  required
                  :error="createErrors.razaoSocial || ''"
                  full-width
                  placeholder="Ex.: Banco do Brasil S.A."
                />
              </div>

              <Input
                v-model="createForm.apelido"
                label="Apelido"
                required
                :error="createErrors.apelido || ''"
                full-width
                placeholder="Ex.: BB"
              />

              <Select
                v-model="createForm.padraoAntecipacao"
                label="Padrão Antecipação"
                required
                :options="padraoOptions"
                :error="createErrors.padraoAntecipacao || ''"
              />
            </form>
          </Card>
        </template>

        <!-- ================================================================
             EDIT CONFIG FORM
        ================================================================ -->
        <template v-else>
          <Card>
            <h3 class="card-section-title">Configuração de Antecipação</h3>
            <form class="form-grid" @submit.prevent="handleSubmit">
              <!-- Booleans -->
              <Checkbox v-model="editForm.aceitaLiquidacaoTotal" label="Aceita Liquidação Total" />
              <Checkbox v-model="editForm.aceitaLiquidacaoParcial" label="Aceita Liquidação Parcial" />
              <Checkbox v-model="editForm.exigeAnuenciaExpressa" label="Exige Anuência Expressa" />
              <Checkbox v-model="editForm.exigeParcelaInteira" label="Exige Parcela Inteira" />

              <!-- Padrão Antecipação -->
              <div class="form-field--full">
                <Select
                  v-model="editForm.padraoAntecipacao"
                  label="Padrão Antecipação"
                  :options="padraoOptions"
                />
              </div>

              <!-- Numbers -->
              <Input
                v-model="editForm.avisoPrevioMinDiasUteis"
                label="Aviso Prévio Mín. (dias úteis)"
                type="number"
                full-width
              />

              <Input
                v-model="editForm.valorMinimoParcialPct"
                label="Valor Mínimo Parcial (%)"
                type="number"
                placeholder="Deixe em branco para não definir"
                full-width
              />

              <Input
                v-model="editForm.breakFundingFeePct"
                label="Break Funding Fee (%)"
                type="number"
                placeholder="Deixe em branco para não definir"
                full-width
              />

              <Input
                v-model="editForm.tlaPctSobreSaldo"
                label="TLA % sobre Saldo"
                type="number"
                placeholder="Deixe em branco para não definir"
                full-width
              />

              <Input
                v-model="editForm.tlaPctPorMesRemanescente"
                label="TLA % por Mês Remanescente"
                type="number"
                placeholder="Deixe em branco para não definir"
                full-width
              />

              <!-- Observations -->
              <div class="form-field--full">
                <Textarea
                  v-model="editForm.observacoesAntecipacao"
                  label="Observações Antecipação"
                  :rows="3"
                  resize="vertical"
                  placeholder="Observações opcionais sobre a política de antecipação deste banco..."
                />
              </div>
            </form>
          </Card>
        </template>

        <!-- Actions -->
        <div class="form-actions">
          <Button variant="ghost" size="md" @click="goBack">Cancelar</Button>
          <Button
            variant="primary"
            size="md"
            :loading="isSubmitting"
            @click="handleSubmit"
          >
            {{ isEditMode ? 'Salvar Configuração' : 'Criar Banco' }}
          </Button>
        </div>
      </div>
    </template>
  </PageLayout>
</template>

<style scoped>
.form-loading,
.form-error {
  padding: 2rem;
}

.form-skeletons {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-width: 700px;
}

.form-content {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  max-width: 700px;
}

.card-section-title {
  font-size: 0.875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary);
  margin: 0 0 1.25rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.form-field--full {
  grid-column: 1 / -1;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
}
</style>

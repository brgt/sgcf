<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import {
  Button,
  Input,
  Select,
  Card,
  Alert,
  Checkbox,
  Spinner,
} from '@nordware/design-system'
import { postIdempotent, extractApiError } from '@/shared/api/client'

// Local alias — SelectOption is not re-exported from the DS root index
interface SelectOption {
  label: string
  value: string | number
  disabled?: boolean
}
import { API } from '@/shared/api/endpoints'
import { ModalidadeContrato, Moeda, BaseCalculo } from '@/shared/api/enums'
import type { ContratoDto } from '@/shared/api/types'
import { useBancosOptions } from '@/shared/api/useBancosOptions'
import { toast } from '@/shared/ui/toast'

// ============================================================================
// Router
// ============================================================================

const router = useRouter()

// ============================================================================
// Step State
// ============================================================================

const step = ref<1 | 2 | 3>(1)
const isSubmitting = ref(false)
const serverError = ref<string | null>(null)

// ============================================================================
// Banco options from API
// ============================================================================

const { data: bancosData, isLoading: bancosLoading } = useBancosOptions()

const bancoOptions = computed<SelectOption[]>(() =>
  (bancosData.value ?? []).map((b) => ({ label: b.apelido, value: b.id })),
)

// ============================================================================
// Static option lists
// ============================================================================

const modalidadeOptions: SelectOption[] = Object.values(ModalidadeContrato).map((v) => ({
  label: v,
  value: v,
}))

const moedaOptions: SelectOption[] = Object.values(Moeda).map((v) => ({
  label: v,
  value: v,
}))

const baseCalculoOptions: SelectOption[] = Object.values(BaseCalculo).map((v) => ({
  label: v,
  value: v,
}))

// ============================================================================
// Step 1 — Dados Básicos
// ============================================================================

const form = reactive({
  numeroExterno: '',
  bancoId: '',
  modalidade: '',
  moeda: '',
  valorPrincipal: null as number | null,
  dataContratacao: '',
  dataVencimento: '',
  taxaAa: null as number | null,
  baseCalculo: 'Du252' as string,
  contratoPaiId: '',
  observacoes: '',
})

// Auto-set moeda to Brl when Nce is selected
watch(
  () => form.modalidade,
  (val) => {
    if (val === ModalidadeContrato.Nce) {
      form.moeda = Moeda.Brl
    }
  },
)

const step1Errors = computed<Record<string, string>>(() => {
  const errors: Record<string, string> = {}
  if (!form.numeroExterno.trim()) errors['numeroExterno'] = 'Obrigatório'
  if (!form.bancoId) errors['bancoId'] = 'Selecione um banco'
  if (!form.modalidade) errors['modalidade'] = 'Selecione uma modalidade'
  if (!form.moeda) errors['moeda'] = 'Selecione uma moeda'
  if (!form.valorPrincipal || form.valorPrincipal <= 0)
    errors['valorPrincipal'] = 'Deve ser maior que zero'
  if (!form.dataContratacao) errors['dataContratacao'] = 'Obrigatório'
  if (!form.dataVencimento) errors['dataVencimento'] = 'Obrigatório'
  if (
    form.dataContratacao &&
    form.dataVencimento &&
    form.dataVencimento <= form.dataContratacao
  )
    errors['dataVencimento'] = 'Deve ser posterior à data de contratação'
  if (!form.taxaAa || form.taxaAa <= 0) errors['taxaAa'] = 'Deve ser maior que zero'
  if (!form.baseCalculo) errors['baseCalculo'] = 'Obrigatório'
  if (form.modalidade === ModalidadeContrato.Refinimp && !form.contratoPaiId.trim())
    errors['contratoPaiId'] = 'Obrigatório para Refinimp'
  if (
    form.modalidade === ModalidadeContrato.Nce &&
    form.moeda &&
    form.moeda !== Moeda.Brl
  )
    errors['moeda'] = 'NCE deve ter moeda BRL'
  return errors
})

const step1Valid = computed(() => Object.keys(step1Errors.value).length === 0)
const step1Touched = ref(false)

// ============================================================================
// Step 2 — Modality-specific details
// ============================================================================

const finimpDetail = reactive({
  rofNumero: '',
  rofDataEmissao: '',
  exportadorNome: '',
  exportadorPais: '',
  produtoImportado: '',
  faturaReferencia: '',
  incoterm: '',
  breakFundingFeePercentual: null as number | null,
  temMarketFlex: false,
})

const lei4131Detail = reactive({
  sblcNumero: '',
  sblcBancoEmissor: '',
  sblcValorUsd: null as number | null,
  temMarketFlex: false,
  breakFundingFeePercentual: null as number | null,
})

const refinimpDetail = reactive({
  contratoMaeId: '',
  percentualRefinanciado: null as number | null,
})

const nceDetail = reactive({
  nceNumero: '',
  dataEmissao: '',
  bancoMandatario: '',
})

const balcaoCaixaDetail = reactive({
  numeroOperacao: '',
  tipoProduto: '',
  temFgi: false,
})

const fgiDetail = reactive({
  numeroOperacaoFgi: '',
  taxaFgiAaPct: null as number | null,
  percentualCobertoPct: null as number | null,
})

// Step 2 validation — only Refinimp has required fields
const step2Errors = computed<Record<string, string>>(() => {
  const errors: Record<string, string> = {}
  if (form.modalidade === ModalidadeContrato.Refinimp) {
    if (!refinimpDetail.contratoMaeId.trim())
      errors['refinimpContratoMaeId'] = 'Obrigatório'
    if (!refinimpDetail.percentualRefinanciado || refinimpDetail.percentualRefinanciado <= 0)
      errors['refinimpPercentual'] = 'Deve ser maior que zero'
    if (
      refinimpDetail.percentualRefinanciado !== null &&
      refinimpDetail.percentualRefinanciado > 100
    )
      errors['refinimpPercentual'] = 'Deve ser no máximo 100'
  }
  return errors
})

const step2Valid = computed(() => Object.keys(step2Errors.value).length === 0)
const step2Touched = ref(false)

// ============================================================================
// Navigation
// ============================================================================

function goNext() {
  if (step.value === 1) {
    step1Touched.value = true
    if (!step1Valid.value) return
    step.value = 2
  } else if (step.value === 2) {
    step2Touched.value = true
    if (!step2Valid.value) return
    step.value = 3
  }
}

function goBack() {
  if (step.value === 2) step.value = 1
  else if (step.value === 3) step.value = 2
}

function cancel() {
  void router.push('/contratos')
}

// ============================================================================
// Build request body
// ============================================================================

function nullIfEmpty(val: string): string | null {
  return val.trim() === '' ? null : val.trim()
}

function buildRequestBody() {
  const m = form.modalidade

  return {
    numeroExterno: form.numeroExterno.trim(),
    bancoId: form.bancoId,
    modalidade: m,
    moeda: form.moeda,
    valorPrincipal: form.valorPrincipal ?? 0,
    dataContratacao: form.dataContratacao,
    dataVencimento: form.dataVencimento,
    taxaAa: form.taxaAa ?? 0,
    baseCalculo: form.baseCalculo,
    contratoPaiId: nullIfEmpty(form.contratoPaiId),
    observacoes: nullIfEmpty(form.observacoes),
    finimpDetail:
      m === ModalidadeContrato.Finimp
        ? {
            rofNumero: nullIfEmpty(finimpDetail.rofNumero),
            rofDataEmissao: nullIfEmpty(finimpDetail.rofDataEmissao),
            exportadorNome: nullIfEmpty(finimpDetail.exportadorNome),
            exportadorPais: nullIfEmpty(finimpDetail.exportadorPais),
            produtoImportado: nullIfEmpty(finimpDetail.produtoImportado),
            faturaReferencia: nullIfEmpty(finimpDetail.faturaReferencia),
            incoterm: nullIfEmpty(finimpDetail.incoterm),
            breakFundingFeePercentual: finimpDetail.breakFundingFeePercentual,
            temMarketFlex: finimpDetail.temMarketFlex,
          }
        : null,
    lei4131Detail:
      m === ModalidadeContrato.Lei4131
        ? {
            sblcNumero: nullIfEmpty(lei4131Detail.sblcNumero),
            sblcBancoEmissor: nullIfEmpty(lei4131Detail.sblcBancoEmissor),
            sblcValorUsd: lei4131Detail.sblcValorUsd,
            temMarketFlex: lei4131Detail.temMarketFlex,
            breakFundingFeePercentual: lei4131Detail.breakFundingFeePercentual,
          }
        : null,
    refinimpDetail:
      m === ModalidadeContrato.Refinimp
        ? {
            contratoMaeId: refinimpDetail.contratoMaeId.trim(),
            percentualRefinanciado: refinimpDetail.percentualRefinanciado ?? 0,
          }
        : null,
    nceDetail:
      m === ModalidadeContrato.Nce
        ? {
            nceNumero: nullIfEmpty(nceDetail.nceNumero),
            dataEmissao: nullIfEmpty(nceDetail.dataEmissao),
            bancoMandatario: nullIfEmpty(nceDetail.bancoMandatario),
          }
        : null,
    balcaoCaixaDetail:
      m === ModalidadeContrato.BalcaoCaixa
        ? {
            numeroOperacao: nullIfEmpty(balcaoCaixaDetail.numeroOperacao),
            tipoProduto: nullIfEmpty(balcaoCaixaDetail.tipoProduto),
            temFgi: balcaoCaixaDetail.temFgi,
          }
        : null,
    fgiDetail:
      m === ModalidadeContrato.Fgi
        ? {
            numeroOperacaoFgi: nullIfEmpty(fgiDetail.numeroOperacaoFgi),
            taxaFgiAaPct: fgiDetail.taxaFgiAaPct,
            percentualCobertoPct: fgiDetail.percentualCobertoPct,
          }
        : null,
  }
}

// ============================================================================
// Submit
// ============================================================================

async function handleSubmit() {
  isSubmitting.value = true
  serverError.value = null
  try {
    const body = buildRequestBody()
    const data = await postIdempotent<ContratoDto>(API.contratos.create, body)
    toast.success('Contrato criado com sucesso!')
    await router.push(`/contratos/${data.id}`)
  } catch (err: unknown) {
    const msg = extractApiError(err)
    serverError.value = msg
    toast.error(msg)
  } finally {
    isSubmitting.value = false
  }
}

// ============================================================================
// Review helpers
// ============================================================================

const reviewRows = computed<{ label: string; value: string }[]>(() => {
  const m = form.modalidade
  const rows: { label: string; value: string }[] = [
    { label: 'Número Externo', value: form.numeroExterno },
    {
      label: 'Banco',
      value:
        bancoOptions.value.find((o) => o.value === form.bancoId)?.label ??
        form.bancoId,
    },
    { label: 'Modalidade', value: m },
    { label: 'Moeda', value: form.moeda },
    {
      label: 'Valor Principal',
      value: form.valorPrincipal != null ? String(form.valorPrincipal) : '—',
    },
    { label: 'Data Contratação', value: form.dataContratacao },
    { label: 'Data Vencimento', value: form.dataVencimento },
    {
      label: 'Taxa a.a. (%)',
      value: form.taxaAa != null ? String(form.taxaAa) : '—',
    },
    { label: 'Base de Cálculo', value: form.baseCalculo },
  ]
  if (form.contratoPaiId.trim())
    rows.push({ label: 'Contrato Mãe (ID)', value: form.contratoPaiId })
  if (form.observacoes.trim())
    rows.push({ label: 'Observações', value: form.observacoes })

  if (m === ModalidadeContrato.Finimp) {
    if (finimpDetail.rofNumero) rows.push({ label: 'ROF Número', value: finimpDetail.rofNumero })
    if (finimpDetail.exportadorNome)
      rows.push({ label: 'Exportador Nome', value: finimpDetail.exportadorNome })
    rows.push({ label: 'Tem Market Flex', value: finimpDetail.temMarketFlex ? 'Sim' : 'Não' })
  } else if (m === ModalidadeContrato.Lei4131) {
    if (lei4131Detail.sblcNumero)
      rows.push({ label: 'SBLC Número', value: lei4131Detail.sblcNumero })
    rows.push({ label: 'Tem Market Flex', value: lei4131Detail.temMarketFlex ? 'Sim' : 'Não' })
  } else if (m === ModalidadeContrato.Refinimp) {
    rows.push({ label: 'Contrato Mãe ID', value: refinimpDetail.contratoMaeId })
    rows.push({
      label: 'Percentual Refinanciado (%)',
      value: refinimpDetail.percentualRefinanciado != null
        ? String(refinimpDetail.percentualRefinanciado)
        : '—',
    })
  } else if (m === ModalidadeContrato.Nce) {
    if (nceDetail.nceNumero) rows.push({ label: 'NCE Número', value: nceDetail.nceNumero })
    if (nceDetail.bancoMandatario)
      rows.push({ label: 'Banco Mandatário', value: nceDetail.bancoMandatario })
  } else if (m === ModalidadeContrato.BalcaoCaixa) {
    rows.push({ label: 'Tem FGI', value: balcaoCaixaDetail.temFgi ? 'Sim' : 'Não' })
  } else if (m === ModalidadeContrato.Fgi) {
    if (fgiDetail.taxaFgiAaPct != null)
      rows.push({ label: 'Taxa FGI a.a. %', value: String(fgiDetail.taxaFgiAaPct) })
    if (fgiDetail.percentualCobertoPct != null)
      rows.push({
        label: 'Percentual Coberto %',
        value: String(fgiDetail.percentualCobertoPct),
      })
  }

  return rows
})
</script>

<template>
  <div class="contrato-create-page">
    <!-- Page header -->
    <div class="page-header">
      <h1 class="page-title">Novo Contrato</h1>
    </div>

    <!-- Stepper indicator -->
    <div class="stepper" aria-label="Progresso do formulário">
      <div
        v-for="(label, idx) in ['Dados Básicos', 'Detalhes', 'Revisão']"
        :key="idx"
        class="stepper__item"
        :class="{
          'stepper__item--active': step === idx + 1,
          'stepper__item--done': step > idx + 1,
        }"
      >
        <div class="stepper__circle" :aria-current="step === idx + 1 ? 'step' : undefined">
          <span v-if="step > idx + 1" class="i-carbon-checkmark stepper__check" aria-hidden="true"></span>
          <span v-else>{{ idx + 1 }}</span>
        </div>
        <span class="stepper__label">{{ label }}</span>
        <div v-if="idx < 2" class="stepper__line" aria-hidden="true"></div>
      </div>
    </div>

    <!-- ================================================================== -->
    <!-- STEP 1: Dados Básicos                                               -->
    <!-- ================================================================== -->
    <div v-if="step === 1">
      <Card title="Dados Básicos do Contrato">
        <div v-if="bancosLoading" class="loading-row">
          <Spinner size="sm" />
          <span>Carregando bancos...</span>
        </div>

        <div class="form-grid">
          <!-- Número Externo -->
          <div class="field">
            <Input
              v-model="form.numeroExterno"
              label="Número Externo"
              placeholder="Ex: FIN-2024-001"
              :error="(step1Touched && step1Errors['numeroExterno']) || ''"
              full-width
            />
          </div>

          <!-- Banco -->
          <div class="field">
            <Select
              v-model="form.bancoId"
              label="Banco"
              :options="bancoOptions"
              placeholder="Selecione um banco"
              :error="(step1Touched && step1Errors['bancoId']) || ''"
            />
          </div>

          <!-- Modalidade -->
          <div class="field">
            <Select
              v-model="form.modalidade"
              label="Modalidade"
              :options="modalidadeOptions"
              placeholder="Selecione uma modalidade"
              :error="(step1Touched && step1Errors['modalidade']) || ''"
            />
          </div>

          <!-- Moeda -->
          <div class="field">
            <Select
              v-model="form.moeda"
              label="Moeda"
              :options="moedaOptions"
              placeholder="Selecione uma moeda"
              :error="(step1Touched && step1Errors['moeda']) || ''"
              :disabled="form.modalidade === 'Nce'"
            />
          </div>

          <!-- Valor Principal -->
          <div class="field">
            <Input
              :model-value="form.valorPrincipal ?? ''"
              label="Valor Principal"
              type="number"
              placeholder="0.00"
              :error="(step1Touched && step1Errors['valorPrincipal']) || ''"
              full-width
              @update:model-value="(v) => (form.valorPrincipal = v === '' ? null : Number(v))"
            />
          </div>

          <!-- Data Contratação (native date) -->
          <div class="field">
            <div class="native-date-field">
              <label class="native-date-label" for="dataContratacao">Data Contratação</label>
              <input
                id="dataContratacao"
                v-model="form.dataContratacao"
                type="date"
                class="native-date-input"
                :class="{ 'native-date-input--error': step1Touched && step1Errors['dataContratacao'] }"
                aria-describedby="dataContratacao-error"
              />
              <span
                v-if="step1Touched && step1Errors['dataContratacao']"
                id="dataContratacao-error"
                class="field-error"
                role="alert"
              >{{ step1Errors['dataContratacao'] }}</span>
            </div>
          </div>

          <!-- Data Vencimento (native date) -->
          <div class="field">
            <div class="native-date-field">
              <label class="native-date-label" for="dataVencimento">Data Vencimento</label>
              <input
                id="dataVencimento"
                v-model="form.dataVencimento"
                type="date"
                class="native-date-input"
                :class="{ 'native-date-input--error': step1Touched && step1Errors['dataVencimento'] }"
                aria-describedby="dataVencimento-error"
              />
              <span
                v-if="step1Touched && step1Errors['dataVencimento']"
                id="dataVencimento-error"
                class="field-error"
                role="alert"
              >{{ step1Errors['dataVencimento'] }}</span>
            </div>
          </div>

          <!-- Taxa a.a. -->
          <div class="field">
            <Input
              :model-value="form.taxaAa ?? ''"
              label="Taxa a.a. (%)"
              type="number"
              placeholder="0.0000"
              :error="(step1Touched && step1Errors['taxaAa']) || ''"
              full-width
              @update:model-value="(v) => (form.taxaAa = v === '' ? null : Number(v))"
            />
          </div>

          <!-- Base de Cálculo -->
          <div class="field">
            <Select
              v-model="form.baseCalculo"
              label="Base de Cálculo"
              :options="baseCalculoOptions"
              placeholder="Selecione"
              :error="(step1Touched && step1Errors['baseCalculo']) || ''"
            />
          </div>

          <!-- Contrato Mãe (only for Refinimp) -->
          <div v-if="form.modalidade === 'Refinimp'" class="field field--full">
            <Input
              v-model="form.contratoPaiId"
              label="Contrato Mãe (ID)"
              placeholder="UUID do contrato original"
              :error="(step1Touched && step1Errors['contratoPaiId']) || ''"
              full-width
            />
          </div>

          <!-- Observações -->
          <div class="field field--full">
            <div class="native-date-field">
              <label class="native-date-label" for="observacoes">Observações (opcional)</label>
              <textarea
                id="observacoes"
                v-model="form.observacoes"
                class="native-textarea"
                placeholder="Informações adicionais..."
                rows="3"
              ></textarea>
            </div>
          </div>
        </div>
      </Card>
    </div>

    <!-- ================================================================== -->
    <!-- STEP 2: Detalhes da Modalidade                                      -->
    <!-- ================================================================== -->
    <div v-else-if="step === 2">

      <!-- FINIMP -->
      <Card
        v-if="form.modalidade === 'Finimp'"
        title="Detalhes FINIMP (Financiamento de Importação)"
      >
        <div class="form-grid">
          <div class="field">
            <Input v-model="finimpDetail.rofNumero" label="ROF Número" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <div class="native-date-field">
              <label class="native-date-label" for="rofDataEmissao">ROF Data Emissão</label>
              <input
                id="rofDataEmissao"
                v-model="finimpDetail.rofDataEmissao"
                type="date"
                class="native-date-input"
              />
            </div>
          </div>
          <div class="field">
            <Input v-model="finimpDetail.exportadorNome" label="Exportador Nome" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input v-model="finimpDetail.exportadorPais" label="Exportador País" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input v-model="finimpDetail.produtoImportado" label="Produto Importado" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input v-model="finimpDetail.faturaReferencia" label="Fatura Referência" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input v-model="finimpDetail.incoterm" label="Incoterm" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input
              :model-value="finimpDetail.breakFundingFeePercentual ?? ''"
              label="Break Funding Fee %"
              type="number"
              placeholder="Opcional"
              full-width
              @update:model-value="(v) => (finimpDetail.breakFundingFeePercentual = v === '' ? null : Number(v))"
            />
          </div>
          <div class="field field--full">
            <Checkbox v-model="finimpDetail.temMarketFlex" label="Tem Market Flex" />
          </div>
        </div>
      </Card>

      <!-- LEI 4131 -->
      <Card v-else-if="form.modalidade === 'Lei4131'" title="Detalhes Lei 4.131">
        <div class="form-grid">
          <div class="field">
            <Input v-model="lei4131Detail.sblcNumero" label="SBLC Número" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input v-model="lei4131Detail.sblcBancoEmissor" label="SBLC Banco Emissor" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input
              :model-value="lei4131Detail.sblcValorUsd ?? ''"
              label="SBLC Valor USD"
              type="number"
              placeholder="Opcional"
              full-width
              @update:model-value="(v) => (lei4131Detail.sblcValorUsd = v === '' ? null : Number(v))"
            />
          </div>
          <div class="field">
            <Input
              :model-value="lei4131Detail.breakFundingFeePercentual ?? ''"
              label="Break Funding Fee %"
              type="number"
              placeholder="Opcional"
              full-width
              @update:model-value="(v) => (lei4131Detail.breakFundingFeePercentual = v === '' ? null : Number(v))"
            />
          </div>
          <div class="field field--full">
            <Checkbox v-model="lei4131Detail.temMarketFlex" label="Tem Market Flex" />
          </div>
        </div>
      </Card>

      <!-- REFINIMP -->
      <Card v-else-if="form.modalidade === 'Refinimp'" title="Detalhes REFINIMP">
        <div class="form-grid">
          <div class="field field--full">
            <Input
              v-model="refinimpDetail.contratoMaeId"
              label="Contrato Mãe ID"
              placeholder="UUID do contrato mãe"
              helper="Mesmo ID inserido em Dados Básicos"
              :error="(step2Touched && step2Errors['refinimpContratoMaeId']) || ''"
              full-width
            />
          </div>
          <div class="field">
            <Input
              :model-value="refinimpDetail.percentualRefinanciado ?? ''"
              label="Percentual Refinanciado (%)"
              type="number"
              placeholder="Ex: 70"
              :error="(step2Touched && step2Errors['refinimpPercentual']) || ''"
              full-width
              @update:model-value="(v) => (refinimpDetail.percentualRefinanciado = v === '' ? null : Number(v))"
            />
          </div>
        </div>
      </Card>

      <!-- NCE -->
      <Card v-else-if="form.modalidade === 'Nce'" title="Detalhes NCE (Nota de Crédito de Exportação)">
        <div class="form-grid">
          <div class="field">
            <Input v-model="nceDetail.nceNumero" label="NCE Número" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <div class="native-date-field">
              <label class="native-date-label" for="nceDataEmissao">Data Emissão</label>
              <input
                id="nceDataEmissao"
                v-model="nceDetail.dataEmissao"
                type="date"
                class="native-date-input"
              />
            </div>
          </div>
          <div class="field">
            <Input v-model="nceDetail.bancoMandatario" label="Banco Mandatário" placeholder="Opcional" full-width />
          </div>
        </div>
      </Card>

      <!-- BALCÃO CAIXA -->
      <Card v-else-if="form.modalidade === 'BalcaoCaixa'" title="Detalhes Balcão Caixa">
        <div class="form-grid">
          <div class="field">
            <Input v-model="balcaoCaixaDetail.numeroOperacao" label="Número Operação" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input v-model="balcaoCaixaDetail.tipoProduto" label="Tipo Produto" placeholder="Opcional" full-width />
          </div>
          <div class="field field--full">
            <Checkbox v-model="balcaoCaixaDetail.temFgi" label="Tem FGI" />
          </div>
        </div>
      </Card>

      <!-- FGI -->
      <Card v-else-if="form.modalidade === 'Fgi'" title="Detalhes FGI">
        <div class="form-grid">
          <div class="field">
            <Input v-model="fgiDetail.numeroOperacaoFgi" label="Número Operação FGI" placeholder="Opcional" full-width />
          </div>
          <div class="field">
            <Input
              :model-value="fgiDetail.taxaFgiAaPct ?? ''"
              label="Taxa FGI a.a. %"
              type="number"
              placeholder="Opcional"
              full-width
              @update:model-value="(v) => (fgiDetail.taxaFgiAaPct = v === '' ? null : Number(v))"
            />
          </div>
          <div class="field">
            <Input
              :model-value="fgiDetail.percentualCobertoPct ?? ''"
              label="Percentual Coberto %"
              type="number"
              placeholder="Opcional"
              full-width
              @update:model-value="(v) => (fgiDetail.percentualCobertoPct = v === '' ? null : Number(v))"
            />
          </div>
        </div>
      </Card>

      <!-- No detail required -->
      <Alert v-else variant="info">
        Nenhum detalhe adicional necessário para esta modalidade.
      </Alert>
    </div>

    <!-- ================================================================== -->
    <!-- STEP 3: Revisão                                                     -->
    <!-- ================================================================== -->
    <div v-else-if="step === 3">
      <Card title="Revisão">
        <p class="review-intro">Revise os dados antes de criar o contrato.</p>

        <Alert v-if="serverError" variant="error" :title="'Erro ao criar contrato'" class="review-error">
          {{ serverError }}
        </Alert>

        <dl class="review-list">
          <div v-for="row in reviewRows" :key="row.label" class="review-row">
            <dt class="review-dt">{{ row.label }}</dt>
            <dd class="review-dd">{{ row.value || '—' }}</dd>
          </div>
        </dl>
      </Card>
    </div>

    <!-- ================================================================== -->
    <!-- Navigation buttons                                                  -->
    <!-- ================================================================== -->
    <div class="nav-buttons">
      <div class="nav-buttons__left">
        <Button v-if="step === 1" variant="ghost" type="button" @click="cancel">
          Cancelar
        </Button>
        <Button v-else variant="secondary" type="button" @click="goBack">
          Voltar
        </Button>
      </div>

      <div class="nav-buttons__right">
        <Button
          v-if="step < 3"
          variant="primary"
          type="button"
          @click="goNext"
        >
          {{ step === 2 ? 'Revisar' : 'Próximo' }}
        </Button>
        <Button
          v-else
          variant="primary"
          type="button"
          :loading="isSubmitting"
          :disabled="isSubmitting"
          @click="handleSubmit"
        >
          Criar Contrato
        </Button>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* Page */
.contrato-create-page {
  max-width: 860px;
  margin: 0 auto;
  padding: 2rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.page-header {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.page-title {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

/* Stepper */
.stepper {
  display: flex;
  align-items: center;
  gap: 0;
}

.stepper__item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
}

.stepper__circle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border-radius: 50%;
  border: 2px solid var(--color-border);
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-tertiary);
  background: var(--color-surface);
  flex-shrink: 0;
  transition: all var(--duration-fast);
}

.stepper__item--active .stepper__circle {
  border-color: var(--color-primary);
  color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
}

.stepper__item--done .stepper__circle {
  border-color: var(--color-success);
  background: var(--color-success);
  color: #fff;
}

.stepper__check {
  font-size: 1rem;
}

.stepper__label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.stepper__item--active .stepper__label {
  color: var(--color-primary);
  font-weight: 600;
}

.stepper__item--done .stepper__label {
  color: var(--color-success);
}

.stepper__line {
  flex: 1;
  height: 2px;
  background: var(--color-border);
  margin: 0 0.75rem;
}

/* Form grid */
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}

.field {
  min-width: 0;
}

.field--full {
  grid-column: 1 / -1;
}

/* Loading row */
.loading-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
  color: var(--color-text-secondary);
  font-size: 0.875rem;
}

/* Native date input (DS Input does not support type="date") */
.native-date-field {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  width: 100%;
}

.native-date-label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.native-date-input {
  width: 100%;
  padding: 0.625rem 1rem;
  font-family: var(--font-family-base);
  font-size: 1rem;
  color: var(--color-text-primary);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  outline: none;
  transition: border-color var(--duration-fast);
  box-sizing: border-box;
}

.native-date-input:hover {
  border-color: var(--color-border-hover);
}

.native-date-input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(0, 249, 184, 0.1);
}

.native-date-input--error {
  border-color: var(--color-error);
}

.native-date-input--error:focus {
  border-color: var(--color-error);
  box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.1);
}

/* Textarea */
.native-textarea {
  width: 100%;
  padding: 0.625rem 1rem;
  font-family: var(--font-family-base);
  font-size: 1rem;
  color: var(--color-text-primary);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  outline: none;
  resize: vertical;
  transition: border-color var(--duration-fast);
  box-sizing: border-box;
}

.native-textarea:hover {
  border-color: var(--color-border-hover);
}

.native-textarea:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(0, 249, 184, 0.1);
}

/* Field error text */
.field-error {
  font-size: 0.75rem;
  color: var(--color-error);
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

/* Review */
.review-intro {
  color: var(--color-text-secondary);
  margin: 0 0 1.25rem;
  font-size: 0.9375rem;
}

.review-error {
  margin-bottom: 1.25rem;
}

.review-list {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0;
}

.review-row {
  display: grid;
  grid-template-columns: 220px 1fr;
  padding: 0.625rem 0;
  border-bottom: 1px solid var(--color-border);
  gap: 1rem;
}

.review-row:last-child {
  border-bottom: none;
}

.review-dt {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.review-dd {
  font-size: 0.875rem;
  color: var(--color-text-primary);
  margin: 0;
  word-break: break-all;
}

/* Navigation buttons */
.nav-buttons {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 0.5rem;
}

.nav-buttons__left,
.nav-buttons__right {
  display: flex;
  gap: 0.75rem;
}

/* Responsive */
@media (max-width: 600px) {
  .form-grid {
    grid-template-columns: 1fr;
  }

  .stepper__label {
    display: none;
  }

  .review-row {
    grid-template-columns: 1fr;
    gap: 0.25rem;
  }
}
</style>

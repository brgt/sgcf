<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  Badge,
  Button,
  DataTable,
  Dropdown,
  Modal,
  Spinner,
  Alert,
  EmptyState,
  Skeleton,
  Tabs,
} from '@nordware/design-system'
import RoleGate from '@/shared/auth/RoleGate.vue'
import { getContrato } from '@/features/contratos/api/useContratos'
import { apiClient, postIdempotent, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import {
  Periodicidade,
  EstruturaAmortizacao,
  AnchorDiaMes,
  ConvencaoDataNaoUtil,
} from '@/shared/api/enums'
import { formatMoney } from '@/shared/money/formatMoney'
import { formatLocalDate } from '@/shared/dates/formatDate'
import { toast } from '@/shared/ui/toast'
import { useConfirm } from '@/shared/ui/useConfirm'
import type { MoedaCode } from '@/shared/money/Money'
import type { StatusGarantia } from '@/shared/api/enums'

// ============================================================================
// Local type aliases
// ============================================================================

type BadgeVariant = 'default' | 'primary' | 'info' | 'success' | 'warning' | 'danger'

interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
}

interface TabItem {
  key: string
  label: string
  icon?: string
}

// ============================================================================
// Router / route
// ============================================================================

const route = useRoute()
const router = useRouter()
const confirm = useConfirm()
const queryClient = useQueryClient()

const id = computed(() => route.params['id'] as string)

// ============================================================================
// Main query
// ============================================================================

const {
  data: contrato,
  isLoading,
  isError,
  error: queryError,
} = useQuery({
  queryKey: computed(() => ['contratos', id.value]),
  queryFn: () => getContrato(id.value),
  enabled: computed(() => !!id.value),
})

// ============================================================================
// Tabs
// ============================================================================

const TABS: TabItem[] = [
  { key: 'dados-gerais', label: 'Dados Gerais',  icon: 'i-carbon-document'      },
  { key: 'cronograma',   label: 'Cronograma',    icon: 'i-carbon-calendar'      },
  { key: 'garantias',    label: 'Garantias',     icon: 'i-carbon-shield'        },
  { key: 'hedges',       label: 'Hedges',        icon: 'i-carbon-exchange-rates' },
]

const activeTab = ref<string>('dados-gerais')

// ============================================================================
// Status helpers
// ============================================================================

function statusBadgeVariant(status: string): BadgeVariant {
  const map: Record<string, BadgeVariant> = {
    Ativo:               'success',
    Liquidado:           'info',
    Vencido:             'warning',
    Inadimplente:        'danger',
    Cancelado:           'danger',
    RefinanciadoParcial: 'default',
    RefinanciadoTotal:   'default',
  }
  return map[status] ?? 'default'
}

function statusLabel(status: string): string {
  const map: Record<string, string> = {
    Ativo:               'Ativo',
    Liquidado:           'Liquidado',
    Vencido:             'Vencido',
    Inadimplente:        'Inadimplente',
    Cancelado:           'Cancelado',
    RefinanciadoParcial: 'Refinanciado Parcial',
    RefinanciadoTotal:   'Refinanciado Total',
  }
  return map[status] ?? status
}

// ============================================================================
// Garantia badge helper
// ============================================================================

function garantiaBadgeVariant(status: StatusGarantia): BadgeVariant {
  const map: Record<StatusGarantia, BadgeVariant> = {
    Ativa:    'success',
    Liberada: 'info',
    Expirada: 'default',
  }
  return map[status]
}

// ============================================================================
// Cronograma (Parcelas) tab
// ============================================================================

interface ParcelaRow {
  id: string
  numero: number
  dataVencimento: string
  valorPrincipal: string
  valorJuros: string
  valorPago: string
  status: string
}

const parcelaColumns: Column<ParcelaRow>[] = [
  { key: 'numero',         label: '#' },
  { key: 'dataVencimento', label: 'Vencimento', sortable: true },
  { key: 'valorPrincipal', label: 'Principal',  align: 'right' },
  { key: 'valorJuros',     label: 'Juros',      align: 'right' },
  { key: 'valorPago',      label: 'Pago',       align: 'right' },
  { key: 'status',         label: 'Status' },
]

const parcelaRows = computed<ParcelaRow[]>(() => {
  const c = contrato.value
  if (!c) return []
  const moeda = c.moeda as MoedaCode
  return [...c.parcelas]
    .sort((a, b) => a.numero - b.numero)
    .map((p) => ({
      id:             p.id,
      numero:         p.numero,
      dataVencimento: formatLocalDate(p.dataVencimento),
      valorPrincipal: formatMoney(p.valorPrincipal, moeda),
      valorJuros:     formatMoney(p.valorJuros, moeda),
      valorPago:      p.valorPago !== null ? formatMoney(p.valorPago, moeda) : '—',
      status:         p.status,
    }))
})

// Gerar Cronograma modal
const showCronogramaModal = ref(false)
const cronogramaLoading = ref(false)
const cronogramaForm = ref({
  aliqIrrfPct:       '',
  aliqIofCambioPct:  '',
  tarifaRofBrl:      '',
  tarifaCadempBrl:   '',
})

async function submitGerarCronograma(): Promise<void> {
  cronogramaLoading.value = true
  try {
    const body: Record<string, number> = {}
    if (cronogramaForm.value.aliqIrrfPct !== '')
      body['aliqIrrfPct'] = parseFloat(cronogramaForm.value.aliqIrrfPct)
    if (cronogramaForm.value.aliqIofCambioPct !== '')
      body['aliqIofCambioPct'] = parseFloat(cronogramaForm.value.aliqIofCambioPct)
    if (cronogramaForm.value.tarifaRofBrl !== '')
      body['tarifaRofBrl'] = parseFloat(cronogramaForm.value.tarifaRofBrl)
    if (cronogramaForm.value.tarifaCadempBrl !== '')
      body['tarifaCadempBrl'] = parseFloat(cronogramaForm.value.tarifaCadempBrl)

    await postIdempotent(API.contratos.gerarCronograma(id.value), body)
    await queryClient.invalidateQueries({ queryKey: ['contratos', id.value] })
    toast.success('Cronograma gerado com sucesso.')
    showCronogramaModal.value = false
    cronogramaForm.value = { aliqIrrfPct: '', aliqIofCambioPct: '', tarifaRofBrl: '', tarifaCadempBrl: '' }
  } catch (err) {
    toast.error(extractApiError(err))
  } finally {
    cronogramaLoading.value = false
  }
}

// ============================================================================
// Garantias tab
// ============================================================================

interface GarantiaRow {
  id: string
  tipo: string
  valorBrl: string
  status: StatusGarantia
  dataConstituicao: string
  dataLiberacaoPrevista: string
}

const garantiaColumns: Column<GarantiaRow>[] = [
  { key: 'tipo',                 label: 'Tipo' },
  { key: 'valorBrl',             label: 'Valor (BRL)', align: 'right' },
  { key: 'status',               label: 'Status' },
  { key: 'dataConstituicao',     label: 'Data Constituição' },
  { key: 'dataLiberacaoPrevista', label: 'Liberação Prevista' },
]

const garantiaRows = computed<GarantiaRow[]>(() =>
  (contrato.value?.garantias ?? []).map((g) => ({
    id:                    g.id,
    tipo:                  g.tipo,
    valorBrl:              formatMoney(g.valorBrl, 'Brl'),
    status:                g.status,
    dataConstituicao:      formatLocalDate(g.dataConstituicao),
    dataLiberacaoPrevista: g.dataLiberacaoPrevista ? formatLocalDate(g.dataLiberacaoPrevista) : '—',
  })),
)

const showGarantiaModal = ref(false)

// ============================================================================
// Hedges tab
// ============================================================================

interface HedgeRow {
  id: string
  tipo: string
  notionalMoedaOriginal: string
  moedaBase: string
  dataVencimento: string
  status: string
}

const hedgeColumns: Column<HedgeRow>[] = [
  { key: 'tipo',                  label: 'Tipo' },
  { key: 'notionalMoedaOriginal', label: 'Notional',       align: 'right' },
  { key: 'moedaBase',             label: 'Moeda Base' },
  { key: 'dataVencimento',        label: 'Data Vencimento' },
  { key: 'status',                label: 'Status' },
]

const hedgeRows = computed<HedgeRow[]>(() =>
  (contrato.value?.hedges ?? []).map((h) => ({
    id:                    h.id,
    tipo:                  h.tipo,
    notionalMoedaOriginal: formatMoney(h.notionalMoedaOriginal, h.moedaBase as MoedaCode),
    moedaBase:             h.moedaBase,
    dataVencimento:        formatLocalDate(h.dataVencimento),
    status:                h.status,
  })),
)

const showHedgeModal = ref(false)

// ============================================================================
// Export dropdown
// ============================================================================

const exportItems = [
  { label: 'Exportar PDF',  value: 'pdf'  },
  { label: 'Exportar XLSX', value: 'xlsx' },
]

function exportar(formato: 'pdf' | 'xlsx'): void {
  const url = `${API.contratos.tabelaCompleta(id.value)}?formato=${formato}`
  window.open(url, '_blank')
}

// ============================================================================
// Delete
// ============================================================================

const deleteLoading = ref(false)

async function handleDelete(): Promise<void> {
  const ok = await confirm({
    title: 'Excluir Contrato',
    message: 'Tem certeza que deseja excluir este contrato? Esta ação não pode ser desfeita.',
    confirmLabel: 'Excluir',
    variant: 'danger',
  })
  if (!ok) return

  deleteLoading.value = true
  try {
    await apiClient.delete(API.contratos.delete(id.value))
    toast.success('Contrato excluído com sucesso.')
    void router.push('/contratos')
  } catch (err) {
    toast.error(extractApiError(err))
  } finally {
    deleteLoading.value = false
  }
}

// ============================================================================
// Edit Amortization Modal
// ============================================================================

interface SelectOption {
  label: string
  value: string
}

const showEditAmortizacaoModal = ref(false)
const editAmortizacaoLoading = ref(false)
const editAmortizacaoForm = ref({
  periodicidade: '',
  estruturaAmortizacao: '',
  quantidadeParcelas: null as number | null,
  dataPrimeiroVencimento: '',
  anchorDiaMes: '',
  anchorDiaFixo: null as number | null,
  periodicidadeJuros: '',
  convencaoDataNaoUtil: '',
})

const periodicidadeOptions: SelectOption[] = Object.values(Periodicidade).map((v) => ({
  label: v,
  value: v,
}))

const estruturaAmortizacaoOptions: SelectOption[] = Object.values(EstruturaAmortizacao).map(
  (v) => ({
    label: v,
    value: v,
  }),
)

const anchorDiaMesOptions: SelectOption[] = Object.values(AnchorDiaMes).map((v) => ({
  label: v,
  value: v,
}))

const convencaoDataNaoUtilOptions: SelectOption[] = Object.values(ConvencaoDataNaoUtil).map(
  (v) => ({
    label: v,
    value: v,
  }),
)

function openEditAmortizacaoModal(): void {
  if (!contrato.value) return
  editAmortizacaoForm.value = {
    periodicidade: contrato.value.periodicidade,
    estruturaAmortizacao: contrato.value.estruturaAmortizacao,
    quantidadeParcelas: contrato.value.quantidadeParcelas,
    dataPrimeiroVencimento: contrato.value.dataPrimeiroVencimento,
    anchorDiaMes: contrato.value.anchorDiaMes,
    anchorDiaFixo: contrato.value.anchorDiaFixo,
    periodicidadeJuros: contrato.value.periodicidadeJuros || '',
    convencaoDataNaoUtil: contrato.value.convencaoDataNaoUtil,
  }
  showEditAmortizacaoModal.value = true
}

async function submitEditAmortizacao(): Promise<void> {
  editAmortizacaoLoading.value = true
  try {
    await apiClient.patch(API.contratos.update(id.value), {
      periodicidade: editAmortizacaoForm.value.periodicidade,
      estruturaAmortizacao: editAmortizacaoForm.value.estruturaAmortizacao,
      quantidadeParcelas: editAmortizacaoForm.value.quantidadeParcelas,
      dataPrimeiroVencimento: editAmortizacaoForm.value.dataPrimeiroVencimento,
      anchorDiaMes: editAmortizacaoForm.value.anchorDiaMes,
      anchorDiaFixo: editAmortizacaoForm.value.anchorDiaFixo,
      periodicidadeJuros: editAmortizacaoForm.value.periodicidadeJuros || null,
      convencaoDataNaoUtil: editAmortizacaoForm.value.convencaoDataNaoUtil,
    })
    await queryClient.invalidateQueries({ queryKey: ['contratos', id.value] })
    toast.success('Amortização atualizada com sucesso.')
    showEditAmortizacaoModal.value = false
  } catch (err) {
    toast.error(extractApiError(err))
  } finally {
    editAmortizacaoLoading.value = false
  }
}

// ============================================================================
// Page header title
// ============================================================================

const pageTitle = computed<string>(() => {
  if (!contrato.value) return 'Carregando...'
  return contrato.value.codigoInterno ?? contrato.value.numeroExterno
})

// ============================================================================
// Dados Gerais helpers
// ============================================================================

function modalidadeLabel(m: string): string {
  const labels: Record<string, string> = {
    Finimp:      'Finimp',
    Refinimp:    'Refinimp',
    Lei4131:     'Lei 4.131',
    Nce:         'NCE',
    BalcaoCaixa: 'Balcão Caixa',
    Fgi:         'FGI',
  }
  return labels[m] ?? m
}
</script>

<template>
  <PageLayout max-width="full">
    <!-- ======================================================================
         Header
    ====================================================================== -->
    <template #header>
      <PageHeader :title="pageTitle">
        <template #actions>
          <Button
            variant="ghost"
            size="sm"
            icon-left="i-carbon-arrow-left"
            @click="() => void router.push('/contratos')"
          >
            Contratos
          </Button>

          <template v-if="contrato">
            <Badge :variant="statusBadgeVariant(contrato.status)" size="sm">
              {{ statusLabel(contrato.status) }}
            </Badge>

            <!-- Export dropdown -->
            <Dropdown
              placement="bottom-end"
              :items="exportItems"
              @select="(item) => exportar(item.value as 'pdf' | 'xlsx')"
            >
              <template #trigger>
                <Button
                  variant="secondary"
                  size="md"
                  icon-left="i-carbon-export"
                >
                  Exportar
                </Button>
              </template>
            </Dropdown>

            <!-- Edit Amortização (Escrita policy) -->
            <RoleGate policy="Escrita">
              <Button
                variant="secondary"
                size="md"
                icon-left="i-carbon-edit"
                @click="openEditAmortizacaoModal"
              >
                Editar Amortização
              </Button>
            </RoleGate>

            <!-- Delete (Gerencial policy) -->
            <RoleGate policy="Gerencial">
              <Button
                variant="danger"
                size="md"
                icon-left="i-carbon-trash-can"
                :loading="deleteLoading"
                @click="() => void handleDelete()"
              >
                Excluir
              </Button>
            </RoleGate>
          </template>
        </template>
      </PageHeader>
    </template>

    <!-- ======================================================================
         Loading state
    ====================================================================== -->
    <div v-if="isLoading" class="detail-loading">
      <Spinner size="lg" />
      <div class="detail-skeletons">
        <Skeleton height="40px" />
        <Skeleton height="200px" />
        <Skeleton height="300px" />
      </div>
    </div>

    <!-- ======================================================================
         Error state
    ====================================================================== -->
    <div v-else-if="isError" class="detail-error">
      <Alert variant="error" title="Erro ao carregar contrato">
        {{ extractApiError(queryError) }}
      </Alert>
    </div>

    <!-- ======================================================================
         Main content
    ====================================================================== -->
    <template v-else-if="contrato">
      <!-- Tabs navigation -->
      <div class="detail-tabs">
        <Tabs
          :items="TABS"
          v-model="activeTab"
        />
      </div>

      <!-- ====================================================================
           TAB 1 — Dados Gerais
      ==================================================================== -->
      <div v-if="activeTab === 'dados-gerais'" class="detail-tab-panel">
        <!-- Main info card -->
        <Card>
          <h3 class="card-section-title">Informações Gerais</h3>
          <div class="info-grid">
            <div class="info-field">
              <span class="info-field__label">Número Externo</span>
              <span class="info-field__value">{{ contrato.numeroExterno }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Código Interno</span>
              <span class="info-field__value">{{ contrato.codigoInterno ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Banco</span>
              <span class="info-field__value info-field__value--mono">{{ contrato.bancoId }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Modalidade</span>
              <span class="info-field__value">{{ modalidadeLabel(contrato.modalidade) }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Moeda</span>
              <span class="info-field__value">{{ contrato.moeda }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Status</span>
              <Badge :variant="statusBadgeVariant(contrato.status)" size="sm">
                {{ statusLabel(contrato.status) }}
              </Badge>
            </div>
            <div class="info-field">
              <span class="info-field__label">Valor Principal</span>
              <span class="info-field__value">{{ formatMoney(contrato.valorPrincipal, contrato.moeda as MoedaCode) }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Data Contratação</span>
              <span class="info-field__value">{{ formatLocalDate(contrato.dataContratacao) }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Data Vencimento</span>
              <span class="info-field__value">{{ formatLocalDate(contrato.dataVencimento) }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Taxa a.a.</span>
              <span class="info-field__value">{{ contrato.taxaAa }}%</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Base de Cálculo</span>
              <span class="info-field__value">{{ contrato.baseCalculo }}</span>
            </div>
            <!-- Sprint 3 Amortization fields -->
            <div class="info-field">
              <span class="info-field__label">Periodicidade</span>
              <span class="info-field__value">{{ contrato.periodicidade }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Estrutura Amortização</span>
              <span class="info-field__value">{{ contrato.estruturaAmortizacao }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Quantidade de Parcelas</span>
              <span class="info-field__value">{{ contrato.quantidadeParcelas }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Data Primeiro Vencimento</span>
              <span class="info-field__value">{{ formatLocalDate(contrato.dataPrimeiroVencimento) }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Âncora Dia Mês</span>
              <span class="info-field__value">{{ contrato.anchorDiaMes }}</span>
            </div>
            <div v-if="contrato.anchorDiaFixo !== null" class="info-field">
              <span class="info-field__label">Dia Fixo (Âncora)</span>
              <span class="info-field__value">{{ contrato.anchorDiaFixo }}</span>
            </div>
            <div v-if="contrato.periodicidadeJuros" class="info-field">
              <span class="info-field__label">Periodicidade Juros</span>
              <span class="info-field__value">{{ contrato.periodicidadeJuros }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Convenção Data Não Útil</span>
              <span class="info-field__value">{{ contrato.convencaoDataNaoUtil }}</span>
            </div>
            <!-- End Sprint 3 -->
            <div v-if="contrato.observacoes" class="info-field info-field--full">
              <span class="info-field__label">Observações</span>
              <span class="info-field__value">{{ contrato.observacoes }}</span>
            </div>
          </div>
        </Card>

        <!-- Modality-specific detail card -->

        <!-- Finimp detail -->
        <Card v-if="contrato.finimpDetail">
          <h3 class="card-section-title">Detalhes Finimp</h3>
          <div class="info-grid">
            <div class="info-field">
              <span class="info-field__label">ROF Número</span>
              <span class="info-field__value">{{ contrato.finimpDetail.rofNumero ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Exportador Nome</span>
              <span class="info-field__value">{{ contrato.finimpDetail.exportadorNome ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Exportador País</span>
              <span class="info-field__value">{{ contrato.finimpDetail.exportadorPais ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Produto Importado</span>
              <span class="info-field__value">{{ contrato.finimpDetail.produtoImportado ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Incoterm</span>
              <span class="info-field__value">{{ contrato.finimpDetail.incoterm ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Break Funding Fee (%)</span>
              <span class="info-field__value">
                {{ contrato.finimpDetail.breakFundingFeePercentual !== null ? `${contrato.finimpDetail.breakFundingFeePercentual}%` : '—' }}
              </span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Market Flex</span>
              <span class="info-field__value">{{ contrato.finimpDetail.temMarketFlex ? 'Sim' : 'Não' }}</span>
            </div>
          </div>
        </Card>

        <!-- Lei 4.131 detail -->
        <Card v-if="contrato.lei4131Detail">
          <h3 class="card-section-title">Detalhes Lei 4.131</h3>
          <div class="info-grid">
            <div class="info-field">
              <span class="info-field__label">SBLC Número</span>
              <span class="info-field__value">{{ contrato.lei4131Detail.sblcNumero ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">SBLC Banco Emissor</span>
              <span class="info-field__value">{{ contrato.lei4131Detail.sblcBancoEmissor ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">SBLC Valor USD</span>
              <span class="info-field__value">
                {{ contrato.lei4131Detail.sblcValorUsd !== null ? formatMoney(contrato.lei4131Detail.sblcValorUsd, 'Usd') : '—' }}
              </span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Break Funding Fee (%)</span>
              <span class="info-field__value">
                {{ contrato.lei4131Detail.breakFundingFeePercentual !== null ? `${contrato.lei4131Detail.breakFundingFeePercentual}%` : '—' }}
              </span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Market Flex</span>
              <span class="info-field__value">{{ contrato.lei4131Detail.temMarketFlex ? 'Sim' : 'Não' }}</span>
            </div>
          </div>
        </Card>

        <!-- Refinimp detail -->
        <Card v-if="contrato.refinimpDetail">
          <h3 class="card-section-title">Detalhes Refinimp</h3>
          <div class="info-grid">
            <div class="info-field">
              <span class="info-field__label">Contrato Pai</span>
              <span class="info-field__value info-field__value--mono">{{ contrato.refinimpDetail.contratoMaeId }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Percentual Refinanciado</span>
              <span class="info-field__value">{{ contrato.refinimpDetail.percentualRefinanciado }}%</span>
            </div>
          </div>
        </Card>

        <!-- NCE detail -->
        <Card v-if="contrato.nceDetail">
          <h3 class="card-section-title">Detalhes NCE</h3>
          <div class="info-grid">
            <div class="info-field">
              <span class="info-field__label">NCE Número</span>
              <span class="info-field__value">{{ contrato.nceDetail.nceNumero ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Banco Mandatário</span>
              <span class="info-field__value">{{ contrato.nceDetail.bancoMandatario ?? '—' }}</span>
            </div>
          </div>
        </Card>

        <!-- Balcão Caixa detail -->
        <Card v-if="contrato.balcaoCaixaDetail">
          <h3 class="card-section-title">Detalhes Balcão Caixa</h3>
          <div class="info-grid">
            <div class="info-field">
              <span class="info-field__label">Número Operação</span>
              <span class="info-field__value">{{ contrato.balcaoCaixaDetail.numeroOperacao ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Tipo Produto</span>
              <span class="info-field__value">{{ contrato.balcaoCaixaDetail.tipoProduto ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Tem FGI</span>
              <span class="info-field__value">{{ contrato.balcaoCaixaDetail.temFgi ? 'Sim' : 'Não' }}</span>
            </div>
          </div>
        </Card>

        <!-- FGI detail -->
        <Card v-if="contrato.fgiDetail">
          <h3 class="card-section-title">Detalhes FGI</h3>
          <div class="info-grid">
            <div class="info-field">
              <span class="info-field__label">Número Operação FGI</span>
              <span class="info-field__value">{{ contrato.fgiDetail.numeroOperacaoFgi ?? '—' }}</span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Taxa FGI a.a. (%)</span>
              <span class="info-field__value">
                {{ contrato.fgiDetail.taxaFgiAaPct !== null ? `${contrato.fgiDetail.taxaFgiAaPct}%` : '—' }}
              </span>
            </div>
            <div class="info-field">
              <span class="info-field__label">Percentual Coberto (%)</span>
              <span class="info-field__value">
                {{ contrato.fgiDetail.percentualCobertoPct !== null ? `${contrato.fgiDetail.percentualCobertoPct}%` : '—' }}
              </span>
            </div>
          </div>
        </Card>
      </div>

      <!-- ====================================================================
           TAB 2 — Cronograma
      ==================================================================== -->
      <div v-else-if="activeTab === 'cronograma'" class="detail-tab-panel">
        <Card>
          <div class="card-header-row">
            <h3 class="card-section-title">Cronograma de Parcelas</h3>
            <RoleGate policy="Escrita">
              <Button
                v-if="contrato.parcelas.length === 0"
                variant="primary"
                size="sm"
                icon-left="i-carbon-add"
                @click="showCronogramaModal = true"
              >
                Gerar Cronograma
              </Button>
            </RoleGate>
          </div>

          <template v-if="contrato.parcelas.length === 0">
            <EmptyState
              icon="i-carbon-calendar"
              title="Nenhuma parcela cadastrada"
              description="Gere o cronograma de parcelas para este contrato."
            />
          </template>
          <template v-else>
            <DataTable
              :columns="parcelaColumns"
              :data="parcelaRows"
            >
              <template #cell-status="{ value }">
                <Badge variant="default" size="sm">{{ String(value) }}</Badge>
              </template>
            </DataTable>
          </template>
        </Card>
      </div>

      <!-- ====================================================================
           TAB 3 — Garantias
      ==================================================================== -->
      <div v-else-if="activeTab === 'garantias'" class="detail-tab-panel">
        <Card>
          <div class="card-header-row">
            <h3 class="card-section-title">Garantias</h3>
            <RoleGate policy="Escrita">
              <Button
                variant="primary"
                size="sm"
                icon-left="i-carbon-add"
                @click="showGarantiaModal = true"
              >
                Adicionar Garantia
              </Button>
            </RoleGate>
          </div>

          <template v-if="contrato.garantias.length === 0">
            <EmptyState
              icon="i-carbon-shield"
              title="Nenhuma garantia cadastrada"
              description="Adicione garantias vinculadas a este contrato."
            />
          </template>
          <template v-else>
            <DataTable
              :columns="garantiaColumns"
              :data="garantiaRows"
            >
              <template #cell-status="{ value }">
                <Badge :variant="garantiaBadgeVariant(value as StatusGarantia)" size="sm">
                  {{ String(value) }}
                </Badge>
              </template>
            </DataTable>
          </template>
        </Card>
      </div>

      <!-- ====================================================================
           TAB 4 — Hedges
      ==================================================================== -->
      <div v-else-if="activeTab === 'hedges'" class="detail-tab-panel">
        <Card>
          <div class="card-header-row">
            <h3 class="card-section-title">Hedges</h3>
            <RoleGate policy="Escrita">
              <Button
                variant="primary"
                size="sm"
                icon-left="i-carbon-add"
                @click="showHedgeModal = true"
              >
                Adicionar Hedge
              </Button>
            </RoleGate>
          </div>

          <template v-if="contrato.hedges.length === 0">
            <EmptyState
              icon="i-carbon-exchange-rates"
              title="Nenhum hedge cadastrado"
              description="Adicione operações de hedge vinculadas a este contrato."
            />
          </template>
          <template v-else>
            <DataTable
              :columns="hedgeColumns"
              :data="hedgeRows"
            />
          </template>
        </Card>
      </div>
    </template>
  </PageLayout>

  <!-- ========================================================================
       Modal — Gerar Cronograma
  ======================================================================== -->
  <Modal
    v-model="showCronogramaModal"
    title="Gerar Cronograma"
    size="md"
  >
    <form class="modal-form" @submit.prevent="() => void submitGerarCronograma()">
      <p class="modal-form__description">
        Preencha os parâmetros opcionais para geração do cronograma. Campos em branco usarão os valores padrão.
      </p>

      <div class="modal-form__grid">
        <label class="form-field">
          <span class="form-field__label">Alíquota IRRF (%)</span>
          <input
            v-model="cronogramaForm.aliqIrrfPct"
            type="number"
            step="0.01"
            min="0"
            class="form-field__input"
            placeholder="Ex.: 15.00"
          />
        </label>
        <label class="form-field">
          <span class="form-field__label">Alíquota IOF Câmbio (%)</span>
          <input
            v-model="cronogramaForm.aliqIofCambioPct"
            type="number"
            step="0.01"
            min="0"
            class="form-field__input"
            placeholder="Ex.: 0.38"
          />
        </label>
        <label class="form-field">
          <span class="form-field__label">Tarifa ROF (BRL)</span>
          <input
            v-model="cronogramaForm.tarifaRofBrl"
            type="number"
            step="0.01"
            min="0"
            class="form-field__input"
            placeholder="Ex.: 500.00"
          />
        </label>
        <label class="form-field">
          <span class="form-field__label">Tarifa CADEMP (BRL)</span>
          <input
            v-model="cronogramaForm.tarifaCadempBrl"
            type="number"
            step="0.01"
            min="0"
            class="form-field__input"
            placeholder="Ex.: 200.00"
          />
        </label>
      </div>
    </form>

    <template #footer>
      <Button variant="ghost" size="md" @click="showCronogramaModal = false">Cancelar</Button>
      <Button
        variant="primary"
        size="md"
        :loading="cronogramaLoading"
        @click="() => void submitGerarCronograma()"
      >
        Gerar
      </Button>
    </template>
  </Modal>

  <!-- ========================================================================
       Modal — Adicionar Garantia (placeholder)
  ======================================================================== -->
  <Modal
    v-model="showGarantiaModal"
    title="Adicionar Garantia"
    size="sm"
  >
    <Alert variant="info" title="Em desenvolvimento">
      O formulário de cadastro de garantias está em desenvolvimento e será disponibilizado em breve.
    </Alert>

    <template #footer>
      <Button variant="primary" size="md" @click="showGarantiaModal = false">Fechar</Button>
    </template>
  </Modal>

  <!-- ========================================================================
       Modal — Adicionar Hedge (placeholder)
  ======================================================================== -->
  <Modal
    v-model="showHedgeModal"
    title="Adicionar Hedge"
    size="sm"
  >
    <Alert variant="info" title="Em desenvolvimento">
      O formulário de cadastro de hedges está em desenvolvimento e será disponibilizado em breve.
    </Alert>

    <template #footer>
      <Button variant="primary" size="md" @click="showHedgeModal = false">Fechar</Button>
    </template>
  </Modal>

  <!-- ========================================================================
       Modal — Editar Amortização (Sprint 3)
  ======================================================================== -->
  <Modal
    v-model="showEditAmortizacaoModal"
    title="Editar Amortização"
    size="md"
  >
    <form class="form">
      <div class="form-grid">
        <label class="form-field">
          <span class="form-field__label">Periodicidade</span>
          <select v-model="editAmortizacaoForm.periodicidade" class="form-field__input">
            <option value="">Selecione</option>
            <option v-for="opt in periodicidadeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </label>

        <label class="form-field">
          <span class="form-field__label">Estrutura Amortização</span>
          <select v-model="editAmortizacaoForm.estruturaAmortizacao" class="form-field__input">
            <option value="">Selecione</option>
            <option v-for="opt in estruturaAmortizacaoOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </label>

        <label class="form-field">
          <span class="form-field__label">Quantidade Parcelas</span>
          <input
            v-model.number="editAmortizacaoForm.quantidadeParcelas"
            type="number"
            min="1"
            class="form-field__input"
            placeholder="0"
          />
        </label>

        <label class="form-field">
          <span class="form-field__label">Data Primeiro Vencimento</span>
          <input
            v-model="editAmortizacaoForm.dataPrimeiroVencimento"
            type="date"
            class="form-field__input"
          />
        </label>

        <label class="form-field">
          <span class="form-field__label">Âncora Dia Mês</span>
          <select v-model="editAmortizacaoForm.anchorDiaMes" class="form-field__input">
            <option value="">Selecione</option>
            <option v-for="opt in anchorDiaMesOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </label>

        <label v-if="editAmortizacaoForm.anchorDiaMes === 'DiaFixo'" class="form-field">
          <span class="form-field__label">Dia Fixo (1-31)</span>
          <input
            v-model.number="editAmortizacaoForm.anchorDiaFixo"
            type="number"
            min="1"
            max="31"
            class="form-field__input"
            placeholder="1"
          />
        </label>

        <label class="form-field">
          <span class="form-field__label">Periodicidade Juros (opcional)</span>
          <select v-model="editAmortizacaoForm.periodicidadeJuros" class="form-field__input">
            <option value="">Deixar em branco</option>
            <option v-for="opt in periodicidadeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </label>

        <label class="form-field">
          <span class="form-field__label">Convenção Data Não Útil</span>
          <select v-model="editAmortizacaoForm.convencaoDataNaoUtil" class="form-field__input">
            <option value="">Selecione</option>
            <option v-for="opt in convencaoDataNaoUtilOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </label>
      </div>
    </form>

    <template #footer>
      <Button variant="ghost" size="md" @click="showEditAmortizacaoModal = false">Cancelar</Button>
      <Button
        variant="primary"
        size="md"
        :loading="editAmortizacaoLoading"
        @click="() => void submitEditAmortizacao()"
      >
        Salvar
      </Button>
    </template>
  </Modal>
</template>

<style scoped>
/* ============================================================================
   Layout
============================================================================ */

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
  max-width: 800px;
}

.detail-error {
  padding: 2rem;
}

.detail-tabs {
  border-bottom: 1px solid var(--color-border, #e5e7eb);
  padding: 0 1.5rem;
}

.detail-tab-panel {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

/* ============================================================================
   Info grid (Dados Gerais)
============================================================================ */

.card-section-title {
  font-size: 0.875rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-secondary, #6b7280);
  margin: 0 0 1rem;
}

.card-header-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 1.25rem;
}

.info-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.info-field--full {
  grid-column: 1 / -1;
}

.info-field__label {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--color-text-secondary, #6b7280);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.info-field__value {
  font-size: 0.9375rem;
  color: var(--color-text-primary, #111827);
}

.info-field__value--mono {
  font-family: ui-monospace, 'Cascadia Code', 'Source Code Pro', monospace;
  font-size: 0.8125rem;
  word-break: break-all;
}

/* ============================================================================
   Modal form
============================================================================ */

.modal-form__description {
  font-size: 0.875rem;
  color: var(--color-text-secondary, #6b7280);
  margin-bottom: 1.25rem;
}

.modal-form__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.form-field__label {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--color-text-primary, #111827);
}

.form-field__input {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border, #d1d5db);
  border-radius: 6px;
  font-size: 0.875rem;
  outline: none;
  transition: border-color 0.15s;
  width: 100%;
}

.form-field__input:focus {
  border-color: var(--color-primary, #3b82f6);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
}
</style>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  DataTable,
  PageLayout,
  PageHeader,
  EmptyState,
  Skeleton,
  Button,
  Badge,
  Modal,
  Alert,
  Select,
  Checkbox,
} from '@nordware/design-system'
import RoleGate from '@/shared/auth/RoleGate.vue'
import { apiClient, postIdempotent, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { toast } from '@/shared/ui/toast'
import { useBancosOptions } from '@/shared/api/useBancosOptions'
import { TipoCotacao, ModalidadeContrato } from '@/shared/api/enums'
import type { ParametroCotacaoDto, CreateParametroCommand } from '@/shared/api/types'

interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
}

interface SelectOption {
  label: string
  value: string
}

type BadgeVariant = 'default' | 'success' | 'warning' | 'danger' | 'info'

// ============================================================================
// Bancos options for lookup
// ============================================================================

const bancosQuery = useBancosOptions()
const bancosMap = computed<Map<string, string>>(() => {
  const map = new Map<string, string>()
  for (const b of bancosQuery.data.value ?? []) {
    map.set(b.id, b.apelido)
  }
  return map
})

const bancosSelectOptions = computed<SelectOption[]>(() =>
  (bancosQuery.data.value ?? []).map((b) => ({ label: b.apelido, value: b.id })),
)

// ============================================================================
// Query
// ============================================================================

const queryClient = useQueryClient()

const { data: parametros, isLoading } = useQuery({
  queryKey: ['parametros-cotacao'] as const,
  queryFn: async (): Promise<ParametroCotacaoDto[]> => {
    const { data } = await apiClient.get<ParametroCotacaoDto[]>(API.parametrosCotacao.list)
    return data
  },
  staleTime: 60_000,
})

const items = computed<ParametroCotacaoDto[]>(() => parametros.value ?? [])
const isEmpty = computed(() => !isLoading.value && items.value.length === 0)

// ============================================================================
// Enum options
// ============================================================================

const tipoCotacaoOptions: SelectOption[] = [
  { label: 'PTAX D1',       value: TipoCotacao.PtaxD1       },
  { label: 'Spot Intraday', value: TipoCotacao.SpotIntraday },
  { label: 'Manual',        value: TipoCotacao.Manual        },
]

const TIPO_COTACAO_LABELS: Record<string, string> = {
  PtaxD1:       'PTAX D1',
  SpotIntraday: 'Spot Intraday',
  Manual:       'Manual',
}

function tipoCotacaoBadge(tipo: string): BadgeVariant {
  const map: Record<string, BadgeVariant> = {
    PtaxD1:       'info',
    SpotIntraday: 'warning',
    Manual:       'default',
  }
  return map[tipo] ?? 'default'
}

const modalidadeOptions: SelectOption[] = [
  { label: 'Finimp',       value: ModalidadeContrato.Finimp      },
  { label: 'Refinimp',     value: ModalidadeContrato.Refinimp    },
  { label: 'Lei 4.131',    value: ModalidadeContrato.Lei4131     },
  { label: 'NCE',          value: ModalidadeContrato.Nce         },
  { label: 'Balcão Caixa', value: ModalidadeContrato.BalcaoCaixa },
  { label: 'FGI',          value: ModalidadeContrato.Fgi         },
]

const MODALIDADE_LABELS: Record<string, string> = {
  Finimp:      'Finimp',
  Refinimp:    'Refinimp',
  Lei4131:     'Lei 4.131',
  Nce:         'NCE',
  BalcaoCaixa: 'Balcão Caixa',
  Fgi:         'FGI',
}

// ============================================================================
// Table
// ============================================================================

interface ParamRow {
  id: string
  tipoCotacao: string
  banco: string
  modalidade: string
  ativo: boolean
}

const tableRows = computed<ParamRow[]>(() =>
  items.value.map((p) => ({
    id: p.id,
    tipoCotacao: p.tipoCotacao,
    banco: p.bancoId ? (bancosMap.value.get(p.bancoId) ?? p.bancoId) : '—',
    modalidade: p.modalidade ? (MODALIDADE_LABELS[p.modalidade] ?? p.modalidade) : '—',
    ativo: p.ativo,
  })),
)

const columns: Column<ParamRow>[] = [
  { key: 'tipoCotacao', label: 'Tipo Cotação', sortable: false },
  { key: 'banco',       label: 'Banco',        sortable: false },
  { key: 'modalidade',  label: 'Modalidade',   sortable: false },
  { key: 'ativo',       label: 'Ativo',        sortable: false },
]

// ============================================================================
// Create modal
// ============================================================================

const showCreateModal = ref(false)
const isCreating = ref(false)
const createError = ref<string | null>(null)

interface CreateForm {
  tipoCotacao: string
  ativo: boolean
  bancoId: string
  modalidade: string
}

const createForm = ref<CreateForm>({
  tipoCotacao: TipoCotacao.PtaxD1,
  ativo: true,
  bancoId: '',
  modalidade: '',
})

function openCreateModal(): void {
  createForm.value = { tipoCotacao: TipoCotacao.PtaxD1, ativo: true, bancoId: '', modalidade: '' }
  createError.value = null
  showCreateModal.value = true
}

async function submitCreate(): Promise<void> {
  isCreating.value = true
  createError.value = null
  try {
    const body: CreateParametroCommand = {
      tipoCotacao: createForm.value.tipoCotacao as CreateParametroCommand['tipoCotacao'],
      ativo: createForm.value.ativo,
      bancoId: createForm.value.bancoId || null,
      modalidade: (createForm.value.modalidade || null) as CreateParametroCommand['modalidade'],
    }
    await postIdempotent(API.parametrosCotacao.create, body)
    await queryClient.invalidateQueries({ queryKey: ['parametros-cotacao'] })
    toast.success('Parâmetro criado com sucesso.')
    showCreateModal.value = false
  } catch (err) {
    createError.value = extractApiError(err)
  } finally {
    isCreating.value = false
  }
}

// ============================================================================
// Edit modal
// ============================================================================

const showEditModal = ref(false)
const isEditing = ref(false)
const editError = ref<string | null>(null)
const editingId = ref<string | null>(null)

interface EditForm {
  tipoCotacao: string
  ativo: boolean
}

const editForm = ref<EditForm>({ tipoCotacao: TipoCotacao.PtaxD1, ativo: true })

function openEditModal(row: ParamRow): void {
  const param = items.value.find((p) => p.id === row.id)
  if (!param) return
  editingId.value = param.id
  editForm.value = { tipoCotacao: param.tipoCotacao, ativo: param.ativo }
  editError.value = null
  showEditModal.value = true
}

async function submitEdit(): Promise<void> {
  if (!editingId.value) return
  isEditing.value = true
  editError.value = null
  try {
    await apiClient.put(API.parametrosCotacao.update(editingId.value), {
      tipoCotacao: editForm.value.tipoCotacao,
      ativo: editForm.value.ativo,
    })
    await queryClient.invalidateQueries({ queryKey: ['parametros-cotacao'] })
    toast.success('Parâmetro atualizado com sucesso.')
    showEditModal.value = false
  } catch (err) {
    editError.value = extractApiError(err)
  } finally {
    isEditing.value = false
  }
}
</script>

<template>
  <PageLayout max-width="full">
    <template #header>
      <PageHeader title="Parâmetros de Cotação">
        <template #actions>
          <RoleGate policy="Admin">
            <Button
              variant="primary"
              size="md"
              icon-left="i-carbon-add"
              @click="openCreateModal"
            >
              Novo Parâmetro
            </Button>
          </RoleGate>
        </template>
      </PageHeader>
    </template>

    <div class="param-list">
      <!-- Loading -->
      <template v-if="isLoading && items.length === 0">
        <div class="param-list__skeletons">
          <Skeleton v-for="n in 5" :key="n" height="48px" />
        </div>
      </template>

      <!-- Empty -->
      <template v-else-if="isEmpty">
        <EmptyState
          icon="i-carbon-settings"
          title="Nenhum parâmetro cadastrado"
          description="Cadastre os parâmetros de cotação para os contratos."
        />
      </template>

      <!-- Table -->
      <template v-else>
        <DataTable
          :columns="columns"
          :data="tableRows"
          :loading="isLoading"
        >
          <template #cell-tipoCotacao="{ row }">
            <Badge :variant="tipoCotacaoBadge((row as ParamRow).tipoCotacao)" size="sm">
              {{ TIPO_COTACAO_LABELS[(row as ParamRow).tipoCotacao] ?? (row as ParamRow).tipoCotacao }}
            </Badge>
          </template>

          <template #cell-ativo="{ row }">
            <Badge :variant="(row as ParamRow).ativo ? 'success' : 'default'" size="sm">
              {{ (row as ParamRow).ativo ? 'Ativo' : 'Inativo' }}
            </Badge>
          </template>

          <template #cell-actions="{ row }">
            <RoleGate policy="Admin">
              <Button
                variant="ghost"
                size="sm"
                @click.stop="openEditModal(row as ParamRow)"
              >
                Editar
              </Button>
            </RoleGate>
          </template>
        </DataTable>
      </template>
    </div>
  </PageLayout>

  <!-- =========================================================================
       Modal — Novo Parâmetro
  ========================================================================= -->
  <Modal v-model="showCreateModal" title="Novo Parâmetro de Cotação" size="md">
    <div class="modal-form">
      <Alert v-if="createError" variant="error" title="Erro ao criar">{{ createError }}</Alert>

      <div class="form-grid">
        <Select
          v-model="createForm.tipoCotacao"
          label="Tipo Cotação"
          :options="tipoCotacaoOptions"
        />

        <div class="form-field--checkbox-container">
          <Checkbox v-model="createForm.ativo" label="Ativo" />
        </div>

        <Select
          v-model="createForm.bancoId"
          label="Banco (opcional)"
          :options="[{ label: 'Nenhum', value: '' }, ...bancosSelectOptions]"
        />

        <Select
          v-model="createForm.modalidade"
          label="Modalidade (opcional)"
          :options="[{ label: 'Nenhuma', value: '' }, ...modalidadeOptions]"
        />
      </div>
    </div>

    <template #footer>
      <Button variant="ghost" size="md" @click="showCreateModal = false">Cancelar</Button>
      <Button variant="primary" size="md" :loading="isCreating" @click="() => void submitCreate()">
        Criar
      </Button>
    </template>
  </Modal>

  <!-- =========================================================================
       Modal — Editar Parâmetro
  ========================================================================= -->
  <Modal v-model="showEditModal" title="Editar Parâmetro" size="sm">
    <div class="modal-form">
      <Alert v-if="editError" variant="error" title="Erro ao atualizar">{{ editError }}</Alert>

      <div class="form-grid">
        <div class="form-field--full">
          <Select
            v-model="editForm.tipoCotacao"
            label="Tipo Cotação"
            :options="tipoCotacaoOptions"
          />
        </div>

        <div class="form-field--full form-field--checkbox-container">
          <Checkbox v-model="editForm.ativo" label="Ativo" />
        </div>
      </div>
    </div>

    <template #footer>
      <Button variant="ghost" size="md" @click="showEditModal = false">Cancelar</Button>
      <Button variant="primary" size="md" :loading="isEditing" @click="() => void submitEdit()">
        Salvar
      </Button>
    </template>
  </Modal>
</template>

<style scoped>
.param-list {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.param-list__skeletons {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.modal-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.875rem;
}

.form-field--full {
  grid-column: 1 / -1;
}

.form-field--checkbox-container {
  display: flex;
  align-items: center;
}
</style>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
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
} from '@nordware/design-system'
import RoleGate from '@/shared/auth/RoleGate.vue'
import { apiClient, postIdempotent, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { toast } from '@/shared/ui/toast'
import { listLancamentos, createLancamento } from '@/features/plano-contas/api/useLancamentos'
import { Moeda } from '@/shared/api/enums'
import type { PlanoContasDto, CreatePlanoContasCommand, AtualizarContaRequest, LancamentoContabilDto, CreateLancamentoRequest } from '@/shared/api/types'

interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
}

type BadgeVariant = 'default' | 'success' | 'warning' | 'danger' | 'info'

// ============================================================================
// Query
// ============================================================================

const queryClient = useQueryClient()

const { data: contas, isLoading } = useQuery({
  queryKey: ['plano-contas'] as const,
  queryFn: async (): Promise<PlanoContasDto[]> => {
    const { data } = await apiClient.get<PlanoContasDto[]>(API.planoContas.list)
    return data
  },
  staleTime: 60_000,
})

const allContas = computed<PlanoContasDto[]>(() => contas.value ?? [])

// ============================================================================
// Filters
// ============================================================================

const search = ref('')
const debouncedSearch = ref('')
const ativoFilter = ref<'todos' | 'ativos' | 'inativos'>('todos')

let debounceTimer: ReturnType<typeof setTimeout> | null = null
watch(search, (val) => {
  if (debounceTimer !== null) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => { debouncedSearch.value = val }, 300)
})

const filteredContas = computed(() => {
  let result = allContas.value

  if (debouncedSearch.value.trim()) {
    const term = debouncedSearch.value.toLowerCase()
    result = result.filter(
      (c) =>
        c.codigoGerencial.toLowerCase().includes(term) ||
        c.nome.toLowerCase().includes(term) ||
        (c.codigoSapB1 ?? '').toLowerCase().includes(term),
    )
  }

  if (ativoFilter.value === 'ativos') {
    result = result.filter((c) => c.ativo)
  } else if (ativoFilter.value === 'inativos') {
    result = result.filter((c) => !c.ativo)
  }

  return result
})

const isEmpty = computed(() => !isLoading.value && filteredContas.value.length === 0)

// ============================================================================
// Table
// ============================================================================

interface ContaRow {
  id: string
  codigoGerencial: string
  nome: string
  natureza: string
  codigoSapB1: string
  ativo: boolean
}

const tableRows = computed<ContaRow[]>(() =>
  filteredContas.value.map((c) => ({
    id: c.id,
    codigoGerencial: c.codigoGerencial,
    nome: c.nome,
    natureza: c.natureza,
    codigoSapB1: c.codigoSapB1 ?? '—',
    ativo: c.ativo,
  })),
)

const columns: Column<ContaRow>[] = [
  { key: 'codigoGerencial', label: 'Código Gerencial', sortable: false },
  { key: 'nome',            label: 'Nome',             sortable: false },
  { key: 'natureza',        label: 'Natureza',         sortable: false },
  { key: 'codigoSapB1',    label: 'Código SAP',       sortable: false },
  { key: 'ativo',           label: 'Ativo',            sortable: false },
]

function ativoBadgeVariant(ativo: boolean): BadgeVariant {
  return ativo ? 'success' : 'default'
}

// ============================================================================
// Create modal
// ============================================================================

const showCreateModal = ref(false)
const isCreating = ref(false)
const createError = ref<string | null>(null)

interface CreateForm {
  codigoGerencial: string
  nome: string
  natureza: string
  codigoSapB1: string
}

const createForm = ref<CreateForm>({
  codigoGerencial: '',
  nome: '',
  natureza: '',
  codigoSapB1: '',
})

const createErrors = ref<Partial<Record<keyof CreateForm, string>>>({})

function validateCreate(): boolean {
  const errs: Partial<Record<keyof CreateForm, string>> = {}
  if (!createForm.value.codigoGerencial.trim()) errs.codigoGerencial = 'Obrigatório'
  if (!createForm.value.nome.trim()) errs.nome = 'Obrigatório'
  if (!createForm.value.natureza.trim()) errs.natureza = 'Obrigatório'
  createErrors.value = errs
  return Object.keys(errs).length === 0
}

function openCreateModal(): void {
  createForm.value = { codigoGerencial: '', nome: '', natureza: '', codigoSapB1: '' }
  createErrors.value = {}
  createError.value = null
  showCreateModal.value = true
}

async function submitCreate(): Promise<void> {
  if (!validateCreate()) return
  isCreating.value = true
  createError.value = null
  try {
    const body: CreatePlanoContasCommand = {
      codigoGerencial: createForm.value.codigoGerencial.trim(),
      nome: createForm.value.nome.trim(),
      natureza: createForm.value.natureza.trim(),
      codigoSapB1: createForm.value.codigoSapB1.trim() || null,
    }
    await postIdempotent(API.planoContas.create, body)
    await queryClient.invalidateQueries({ queryKey: ['plano-contas'] })
    toast.success('Conta criada com sucesso.')
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
  nome: string
  natureza: string
  codigoSapB1: string
}

const editForm = ref<EditForm>({ nome: '', natureza: '', codigoSapB1: '' })
const editErrors = ref<Partial<Record<keyof EditForm, string>>>({})

function validateEdit(): boolean {
  const errs: Partial<Record<keyof EditForm, string>> = {}
  if (!editForm.value.nome.trim()) errs.nome = 'Obrigatório'
  if (!editForm.value.natureza.trim()) errs.natureza = 'Obrigatório'
  editErrors.value = errs
  return Object.keys(errs).length === 0
}

function openEditModal(row: ContaRow): void {
  const conta = allContas.value.find((c) => c.id === row.id)
  if (!conta) return
  editingId.value = conta.id
  editForm.value = {
    nome: conta.nome,
    natureza: conta.natureza,
    codigoSapB1: conta.codigoSapB1 ?? '',
  }
  editErrors.value = {}
  editError.value = null
  showEditModal.value = true
}

async function submitEdit(): Promise<void> {
  if (!editingId.value || !validateEdit()) return
  isEditing.value = true
  editError.value = null
  try {
    const body: AtualizarContaRequest = {
      nome: editForm.value.nome.trim(),
      natureza: editForm.value.natureza.trim(),
      codigoSapB1: editForm.value.codigoSapB1.trim() || null,
    }
    await apiClient.put(API.planoContas.update(editingId.value), body)
    await queryClient.invalidateQueries({ queryKey: ['plano-contas'] })
    toast.success('Conta atualizada com sucesso.')
    showEditModal.value = false
  } catch (err) {
    editError.value = extractApiError(err)
  } finally {
    isEditing.value = false
  }
}

// ============================================================================
// Lancamentos modal
// ============================================================================

const showLancamentosModal = ref(false)
const lancamentosContaId = ref<string | null>(null)
const lancamentos = ref<LancamentoContabilDto[]>([])
const isLoadingLancamentos = ref(false)
const lancamentosError = ref<string | null>(null)

const isCreatingLancamento = ref(false)
const createLancamentoError = ref<string | null>(null)

interface CreateLancamentoForm {
  contratoId: string
  data: string
  origem: string
  valorDecimal: string
  moeda: typeof Moeda[keyof typeof Moeda]
  descricao: string
}

const createLancamentoForm = ref<CreateLancamentoForm>({
  contratoId: '',
  data: '',
  origem: '',
  valorDecimal: '',
  moeda: Moeda.Brl,
  descricao: '',
})

const createLancamentoErrors = ref<Partial<Record<keyof CreateLancamentoForm, string>>>({})

function validateCreateLancamento(): boolean {
  const errs: Partial<Record<keyof CreateLancamentoForm, string>> = {}
  if (!createLancamentoForm.value.contratoId.trim()) errs.contratoId = 'Obrigatório'
  if (!createLancamentoForm.value.data.trim()) errs.data = 'Obrigatório'
  if (!createLancamentoForm.value.origem.trim()) errs.origem = 'Obrigatório'
  if (!createLancamentoForm.value.valorDecimal.trim()) errs.valorDecimal = 'Obrigatório'
  if (isNaN(Number(createLancamentoForm.value.valorDecimal))) errs.valorDecimal = 'Deve ser um número'
  createLancamentoErrors.value = errs
  return Object.keys(errs).length === 0
}

async function openLancamentosModal(row: ContaRow): Promise<void> {
  lancamentosContaId.value = row.id
  lancamentos.value = []
  lancamentosError.value = null
  isLoadingLancamentos.value = true
  showLancamentosModal.value = true

  try {
    lancamentos.value = await listLancamentos(row.id)
  } catch (err) {
    lancamentosError.value = extractApiError(err)
  } finally {
    isLoadingLancamentos.value = false
  }
}

async function submitCreateLancamento(): Promise<void> {
  if (!lancamentosContaId.value || !validateCreateLancamento()) return
  isCreatingLancamento.value = true
  createLancamentoError.value = null
  try {
    const body: CreateLancamentoRequest = {
      contratoId: createLancamentoForm.value.contratoId.trim(),
      data: createLancamentoForm.value.data,
      origem: createLancamentoForm.value.origem.trim(),
      valorDecimal: Number(createLancamentoForm.value.valorDecimal),
      moeda: createLancamentoForm.value.moeda,
      descricao: createLancamentoForm.value.descricao.trim(),
    }
    const newLancamento = await createLancamento(lancamentosContaId.value, body)
    lancamentos.value.push(newLancamento)
    toast.success('Lançamento criado com sucesso.')
    createLancamentoForm.value = {
      contratoId: '',
      data: '',
      origem: '',
      valorDecimal: '',
      moeda: Moeda.Brl,
      descricao: '',
    }
    createLancamentoErrors.value = {}
  } catch (err) {
    createLancamentoError.value = extractApiError(err)
  } finally {
    isCreatingLancamento.value = false
  }
}
</script>

<template>
  <PageLayout max-width="full">
    <template #header>
      <PageHeader title="Plano de Contas">
        <template #actions>
          <RoleGate policy="Auditoria">
            <Button
              variant="primary"
              size="md"
              icon-left="i-carbon-add"
              @click="openCreateModal"
            >
              Nova Conta
            </Button>
          </RoleGate>
        </template>
      </PageHeader>
    </template>

    <div class="plano-list">
      <!-- Toolbar -->
      <div class="plano-list__toolbar">
        <input
          v-model="search"
          type="text"
          class="plano-list__search"
          placeholder="Buscar por código, nome ou SAP..."
          aria-label="Buscar contas"
        />
        <select v-model="ativoFilter" class="plano-list__select" aria-label="Filtrar por status">
          <option value="todos">Todos</option>
          <option value="ativos">Ativos</option>
          <option value="inativos">Inativos</option>
        </select>
      </div>

      <!-- Loading -->
      <template v-if="isLoading && allContas.length === 0">
        <div class="plano-list__skeletons">
          <Skeleton v-for="n in 6" :key="n" height="48px" />
        </div>
      </template>

      <!-- Empty -->
      <template v-else-if="isEmpty">
        <EmptyState
          icon="i-carbon-document"
          title="Nenhuma conta encontrada"
          description="Ajuste os filtros ou cadastre uma nova conta."
        />
      </template>

      <!-- Table -->
      <template v-else>
        <DataTable
          :columns="columns"
          :data="tableRows"
          :loading="isLoading"
        >
          <template #cell-ativo="{ row }">
            <Badge :variant="ativoBadgeVariant((row as ContaRow).ativo)" size="sm">
              {{ (row as ContaRow).ativo ? 'Ativo' : 'Inativo' }}
            </Badge>
          </template>

          <template #cell-actions="{ row }">
            <RoleGate policy="Auditoria">
              <div class="flex gap-2">
                <Button
                  variant="ghost"
                  size="sm"
                  @click.stop="openLancamentosModal(row as ContaRow)"
                >
                  Lançamentos
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  @click.stop="openEditModal(row as ContaRow)"
                >
                  Editar
                </Button>
              </div>
            </RoleGate>
          </template>
        </DataTable>
      </template>
    </div>
  </PageLayout>

  <!-- =========================================================================
       Modal — Nova Conta
  ========================================================================= -->
  <Modal v-model="showCreateModal" title="Nova Conta" size="md">
    <div class="modal-form">
      <Alert v-if="createError" variant="error" title="Erro ao criar">{{ createError }}</Alert>

      <div class="form-grid">
        <label class="form-field">
          <span class="form-field__label">Código Gerencial <span class="form-field__required">*</span></span>
          <input
            v-model="createForm.codigoGerencial"
            type="text"
            class="form-field__input"
            :class="{ 'form-field__input--error': createErrors.codigoGerencial }"
            placeholder="Ex.: 3.01.001"
          />
          <span v-if="createErrors.codigoGerencial" class="form-field__error">
            {{ createErrors.codigoGerencial }}
          </span>
        </label>

        <label class="form-field">
          <span class="form-field__label">Natureza <span class="form-field__required">*</span></span>
          <input
            v-model="createForm.natureza"
            type="text"
            class="form-field__input"
            :class="{ 'form-field__input--error': createErrors.natureza }"
            placeholder="Ex.: Passivo"
          />
          <span v-if="createErrors.natureza" class="form-field__error">
            {{ createErrors.natureza }}
          </span>
        </label>

        <label class="form-field form-field--full">
          <span class="form-field__label">Nome <span class="form-field__required">*</span></span>
          <input
            v-model="createForm.nome"
            type="text"
            class="form-field__input"
            :class="{ 'form-field__input--error': createErrors.nome }"
            placeholder="Ex.: Empréstimos e Financiamentos"
          />
          <span v-if="createErrors.nome" class="form-field__error">{{ createErrors.nome }}</span>
        </label>

        <label class="form-field form-field--full">
          <span class="form-field__label">Código SAP B1</span>
          <input
            v-model="createForm.codigoSapB1"
            type="text"
            class="form-field__input"
            placeholder="Opcional"
          />
        </label>
      </div>
    </div>

    <template #footer>
      <Button variant="ghost" size="md" @click="showCreateModal = false">Cancelar</Button>
      <Button variant="primary" size="md" :loading="isCreating" @click="() => void submitCreate()">
        Criar Conta
      </Button>
    </template>
  </Modal>

  <!-- =========================================================================
       Modal — Editar Conta
  ========================================================================= -->
  <Modal v-model="showEditModal" title="Editar Conta" size="md">
    <div class="modal-form">
      <Alert v-if="editError" variant="error" title="Erro ao atualizar">{{ editError }}</Alert>

      <div class="form-grid">
        <label class="form-field form-field--full">
          <span class="form-field__label">Nome <span class="form-field__required">*</span></span>
          <input
            v-model="editForm.nome"
            type="text"
            class="form-field__input"
            :class="{ 'form-field__input--error': editErrors.nome }"
          />
          <span v-if="editErrors.nome" class="form-field__error">{{ editErrors.nome }}</span>
        </label>

        <label class="form-field">
          <span class="form-field__label">Natureza <span class="form-field__required">*</span></span>
          <input
            v-model="editForm.natureza"
            type="text"
            class="form-field__input"
            :class="{ 'form-field__input--error': editErrors.natureza }"
          />
          <span v-if="editErrors.natureza" class="form-field__error">{{ editErrors.natureza }}</span>
        </label>

        <label class="form-field">
          <span class="form-field__label">Código SAP B1</span>
          <input
            v-model="editForm.codigoSapB1"
            type="text"
            class="form-field__input"
            placeholder="Opcional"
          />
        </label>
      </div>
    </div>

    <template #footer>
      <Button variant="ghost" size="md" @click="showEditModal = false">Cancelar</Button>
      <Button variant="primary" size="md" :loading="isEditing" @click="() => void submitEdit()">
        Salvar
      </Button>
    </template>
  </Modal>

  <!-- =========================================================================
       Modal — Lançamentos Contábeis
  ========================================================================= -->
  <Modal v-model="showLancamentosModal" title="Lançamentos Contábeis" size="lg">
    <div class="modal-form">
      <!-- Error -->
      <Alert v-if="lancamentosError" variant="error" title="Erro ao carregar">
        {{ lancamentosError }}
      </Alert>

      <!-- Loading -->
      <div v-if="isLoadingLancamentos" class="text-center py-8">
        <Skeleton height="40px" />
      </div>

      <!-- Lancamentos list -->
      <template v-else>
        <div v-if="lancamentos.length === 0" class="text-center py-4 text-sm text-gray-500">
          Nenhum lançamento registrado
        </div>

        <div v-else class="lancamentos-list">
          <div v-for="lance in lancamentos" :key="lance.id" class="lancamento-item">
            <div class="lancamento-row">
              <span class="lancamento-label">Data:</span>
              <span class="lancamento-value">{{ lance.data }}</span>
            </div>
            <div class="lancamento-row">
              <span class="lancamento-label">Origem:</span>
              <span class="lancamento-value">{{ lance.origem }}</span>
            </div>
            <div class="lancamento-row">
              <span class="lancamento-label">Valor:</span>
              <span class="lancamento-value">{{ lance.valor.toFixed(2) }} {{ lance.moeda }}</span>
            </div>
            <div class="lancamento-row">
              <span class="lancamento-label">Descrição:</span>
              <span class="lancamento-value">{{ lance.descricao }}</span>
            </div>
          </div>
        </div>
      </template>

      <!-- Create form -->
      <div v-if="!isLoadingLancamentos" class="mt-6 pt-6 border-t border-gray-200">
        <h4 class="text-sm font-semibold mb-4">Novo Lançamento</h4>

        <Alert v-if="createLancamentoError" variant="error" title="Erro ao criar">
          {{ createLancamentoError }}
        </Alert>

        <div class="form-grid">
          <label class="form-field">
            <span class="form-field__label">Contrato <span class="form-field__required">*</span></span>
            <input
              v-model="createLancamentoForm.contratoId"
              type="text"
              class="form-field__input"
              :class="{ 'form-field__input--error': createLancamentoErrors.contratoId }"
              placeholder="ID do contrato"
            />
            <span v-if="createLancamentoErrors.contratoId" class="form-field__error">
              {{ createLancamentoErrors.contratoId }}
            </span>
          </label>

          <label class="form-field">
            <span class="form-field__label">Data <span class="form-field__required">*</span></span>
            <input
              v-model="createLancamentoForm.data"
              type="date"
              class="form-field__input"
              :class="{ 'form-field__input--error': createLancamentoErrors.data }"
            />
            <span v-if="createLancamentoErrors.data" class="form-field__error">
              {{ createLancamentoErrors.data }}
            </span>
          </label>

          <label class="form-field">
            <span class="form-field__label">Origem <span class="form-field__required">*</span></span>
            <input
              v-model="createLancamentoForm.origem"
              type="text"
              class="form-field__input"
              :class="{ 'form-field__input--error': createLancamentoErrors.origem }"
              placeholder="Ex.: Manual, Automático"
            />
            <span v-if="createLancamentoErrors.origem" class="form-field__error">
              {{ createLancamentoErrors.origem }}
            </span>
          </label>

          <label class="form-field">
            <span class="form-field__label">Valor <span class="form-field__required">*</span></span>
            <input
              v-model="createLancamentoForm.valorDecimal"
              type="number"
              class="form-field__input"
              :class="{ 'form-field__input--error': createLancamentoErrors.valorDecimal }"
              placeholder="0.00"
              step="0.01"
            />
            <span v-if="createLancamentoErrors.valorDecimal" class="form-field__error">
              {{ createLancamentoErrors.valorDecimal }}
            </span>
          </label>

          <label class="form-field">
            <span class="form-field__label">Moeda</span>
            <select v-model="createLancamentoForm.moeda" class="form-field__input">
              <option :value="Moeda.Brl">BRL</option>
              <option :value="Moeda.Usd">USD</option>
              <option :value="Moeda.Eur">EUR</option>
            </select>
          </label>

          <label class="form-field form-field--full">
            <span class="form-field__label">Descrição</span>
            <input
              v-model="createLancamentoForm.descricao"
              type="text"
              class="form-field__input"
              placeholder="Descrição do lançamento"
            />
          </label>
        </div>
      </div>
    </div>

    <template #footer>
      <Button variant="ghost" size="md" @click="showLancamentosModal = false">Fechar</Button>
      <Button
        v-if="!isLoadingLancamentos"
        variant="primary"
        size="md"
        :loading="isCreatingLancamento"
        @click="() => void submitCreateLancamento()"
      >
        Criar Lançamento
      </Button>
    </template>
  </Modal>
</template>

<style scoped>
.plano-list {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.plano-list__toolbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.plano-list__search {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border, #d1d5db);
  border-radius: 6px;
  font-size: 0.875rem;
  outline: none;
  width: 280px;
  transition: border-color 0.15s;
}

.plano-list__search:focus {
  border-color: var(--color-primary, #3b82f6);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
}

.plano-list__select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border, #d1d5db);
  border-radius: 6px;
  font-size: 0.875rem;
  outline: none;
  background: var(--color-surface, #ffffff);
  color: var(--color-text-primary, #111827);
  cursor: pointer;
}

.plano-list__select:focus {
  border-color: var(--color-primary, #3b82f6);
}

.plano-list__skeletons {
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

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.form-field--full {
  grid-column: 1 / -1;
}

.form-field__label {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--color-text-primary, #111827);
}

.form-field__required {
  color: var(--color-error, #ef4444);
}

.form-field__input {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border, #d1d5db);
  border-radius: 6px;
  font-size: 0.875rem;
  outline: none;
  width: 100%;
  background: var(--color-surface, #ffffff);
  color: var(--color-text-primary, #111827);
  transition: border-color 0.15s;
}

.form-field__input:focus {
  border-color: var(--color-primary, #3b82f6);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
}

.form-field__input--error {
  border-color: var(--color-error, #ef4444);
}

.form-field__error {
  font-size: 0.75rem;
  color: var(--color-error, #ef4444);
}

.lancamentos-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-height: 300px;
  overflow-y: auto;
}

.lancamento-item {
  padding: 0.75rem;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 6px;
  background: var(--color-surface, #f9fafb);
}

.lancamento-row {
  display: flex;
  justify-content: space-between;
  padding: 0.25rem 0;
  font-size: 0.875rem;
}

.lancamento-label {
  font-weight: 500;
  color: var(--color-text-secondary, #6b7280);
}

.lancamento-value {
  color: var(--color-text-primary, #111827);
}
</style>

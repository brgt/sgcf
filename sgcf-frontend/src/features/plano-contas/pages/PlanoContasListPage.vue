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
import type { PlanoContasDto, CreatePlanoContasCommand, AtualizarContaRequest } from '@/shared/api/types'

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
              <Button
                variant="ghost"
                size="sm"
                @click.stop="openEditModal(row as ContaRow)"
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
</style>

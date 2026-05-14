<script setup lang="ts">
import { ref, computed } from 'vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  DataTable,
  Button,
  Modal,
  Spinner,
  Alert,
  EmptyState,
} from '@nordware/design-system'
import { listFeriados, createFeriado } from '@/features/feriados/api/useFeriados'
import { EscopoFeriado, TipoFeriado } from '@/shared/api/enums'
import type { CreateFeriadoRequest } from '@/shared/api/types'
import { formatLocalDate } from '@/shared/dates/formatDate'
import { toast } from '@/shared/ui/toast'
import { extractApiError } from '@/shared/api/client'

// ============================================================================
// Local type aliases
// ============================================================================

interface SelectOption {
  label: string
  value: string
}

interface FeriadoRow {
  id: string
  data: string
  descricao: string
  abrangencia: string
  tipo: string
  fonte: string
}

interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
}

// ============================================================================
// Setup
// ============================================================================

const queryClient = useQueryClient()

// ============================================================================
// Query: list feriados
// ============================================================================

const {
  data: feriadosData,
  isLoading,
  isError,
  error: queryError,
} = useQuery({
  queryKey: ['feriados'],
  queryFn: listFeriados,
})

const feriadoRows = computed<FeriadoRow[]>(() => {
  const feriados = feriadosData.value?.items ?? []
  return feriados.map((f) => ({
    id: f.id,
    data: formatLocalDate(f.data),
    descricao: f.descricao,
    abrangencia: f.abrangencia,
    tipo: f.tipo,
    fonte: f.fonte,
  }))
})

const columns: Column<FeriadoRow>[] = [
  { key: 'data', label: 'Data', sortable: true },
  { key: 'descricao', label: 'Descrição' },
  { key: 'abrangencia', label: 'Abrangência' },
  { key: 'tipo', label: 'Tipo' },
  { key: 'fonte', label: 'Fonte' },
]

// ============================================================================
// Modal: Create Feriado
// ============================================================================

const showCreateModal = ref(false)
const createLoading = ref(false)
const createForm = ref<CreateFeriadoRequest>({
  data: '',
  descricao: '',
  abrangencia: '' as any,
  tipo: '' as any,
})

const escopoOptions: SelectOption[] = Object.values(EscopoFeriado).map((v) => ({
  label: v,
  value: v,
}))

const tipoOptions: SelectOption[] = Object.values(TipoFeriado).map((v) => ({
  label: v,
  value: v,
}))

async function submitCreate(): Promise<void> {
  createLoading.value = true
  try {
    await createFeriado(createForm.value)
    await queryClient.invalidateQueries({ queryKey: ['feriados'] })
    toast.success('Feriado criado com sucesso.')
    showCreateModal.value = false
    createForm.value = { data: '', descricao: '', abrangencia: '' as any, tipo: '' as any }
  } catch (err) {
    toast.error(extractApiError(err))
  } finally {
    createLoading.value = false
  }
}

</script>

<template>
  <PageLayout>
    <!-- Header -->
    <template #header>
      <PageHeader title="Feriados">
        <template #actions>
          <Button
            variant="primary"
            size="md"
            icon-left="i-carbon-add"
            @click="showCreateModal = true"
          >
            Novo Feriado
          </Button>
        </template>
      </PageHeader>
    </template>

    <!-- Loading state -->
    <div v-if="isLoading" class="loading-state">
      <Spinner size="lg" />
      <span>Carregando feriados...</span>
    </div>

    <!-- Error state -->
    <Alert v-else-if="isError" variant="error" title="Erro ao carregar feriados">
      {{ extractApiError(queryError) }}
    </Alert>

    <!-- Empty state -->
    <EmptyState
      v-else-if="feriadoRows.length === 0"
      title="Nenhum feriado"
      description="Não há feriados cadastrados. Clique no botão acima para criar um novo feriado."
      icon="i-carbon-calendar"
    />

    <!-- Data table -->
    <template v-else>
      <Card>
        <DataTable
          :columns="columns"
          :rows="feriadoRows"
          :loading="isLoading"
        >
          <template #cell-data="{ row }">
            {{ row.data }}
          </template>

          <template #cell-descricao="{ row }">
            {{ row.descricao }}
          </template>

          <template #cell-abrangencia="{ row }">
            {{ row.abrangencia }}
          </template>

          <template #cell-tipo="{ row }">
            {{ row.tipo }}
          </template>

          <template #cell-fonte="{ row }">
            {{ row.fonte }}
          </template>
        </DataTable>
      </Card>
    </template>

    <!-- Modal: Create Feriado -->
    <Modal
      v-model="showCreateModal"
      title="Novo Feriado"
      size="sm"
    >
      <form class="form">
        <div class="form-grid">
          <label class="form-field">
            <span class="form-field__label">Data</span>
            <input
              v-model="createForm.data"
              type="date"
              class="form-field__input"
              placeholder="2025-12-25"
            />
          </label>

          <label class="form-field">
            <span class="form-field__label">Descrição</span>
            <input
              v-model="createForm.descricao"
              type="text"
              class="form-field__input"
              placeholder="Ex: Natal"
            />
          </label>

          <label class="form-field">
            <span class="form-field__label">Abrangência</span>
            <select v-model="createForm.abrangencia" class="form-field__input">
              <option value="">Selecione</option>
              <option v-for="opt in escopoOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </option>
            </select>
          </label>

          <label class="form-field">
            <span class="form-field__label">Tipo</span>
            <select v-model="createForm.tipo" class="form-field__input">
              <option value="">Selecione</option>
              <option v-for="opt in tipoOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </option>
            </select>
          </label>
        </div>
      </form>

      <template #footer>
        <Button variant="ghost" size="md" @click="showCreateModal = false">Cancelar</Button>
        <Button
          variant="primary"
          size="md"
          :loading="createLoading"
          @click="() => void submitCreate()"
        >
          Criar
        </Button>
      </template>
    </Modal>
  </PageLayout>
</template>

<style scoped>
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  padding: 4rem 2rem;
  min-height: 400px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-field__label {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary);
}

.form-field__input {
  padding: 0.625rem 0.75rem;
  font-family: var(--font-family-base);
  font-size: 0.875rem;
  color: var(--color-text-primary);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  transition: border-color var(--duration-fast);
  outline: none;
}

.form-field__input:hover {
  border-color: var(--color-border-hover);
}

.form-field__input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(0, 249, 184, 0.1);
}
</style>

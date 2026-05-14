<script setup lang="ts">
import { ref, computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import {
  PageLayout,
  PageHeader,
  Card,
  DataTable,
  Badge,
  Skeleton,
  Alert,
  EmptyState,
} from '@nordware/design-system'
import { listAuditEvento } from '@/features/auditoria/api/useAuditoria'
import { extractApiError } from '@/shared/api/client'
import type { AuditEventoDto, AuditFilter } from '@/shared/api/types'

interface Column<T> {
  key: keyof T
  label: string
  sortable?: boolean
  align?: 'left' | 'center' | 'right'
}

type OperationBadgeVariant = 'default' | 'success' | 'warning' | 'danger' | 'info'
type SourceBadgeVariant = 'default' | 'success' | 'warning' | 'danger' | 'info'

// ============================================================================
// Filters
// ============================================================================

const entityFilter = ref('')
const operationFilter = ref<'' | 'CREATE' | 'UPDATE' | 'DELETE'>('')
const sourceFilter = ref<'' | 'rest' | 'mcp' | 'a2a' | 'job'>('')
const actorSubFilter = ref('')

// ============================================================================
// Query
// ============================================================================

const { data: eventos, isLoading, isError, error: queryError } = useQuery({
  queryKey: [
    'auditoria-eventos',
    entityFilter,
    operationFilter,
    sourceFilter,
    actorSubFilter,
  ] as const,
  queryFn: async (): Promise<AuditEventoDto[]> => {
    const filter: AuditFilter = {}
    if (entityFilter.value.trim()) filter.entity = entityFilter.value.trim()
    if (operationFilter.value) filter.operation = operationFilter.value
    if (sourceFilter.value) filter.source = sourceFilter.value
    if (actorSubFilter.value.trim()) filter.actorSub = actorSubFilter.value.trim()

    return listAuditEvento(Object.keys(filter).length > 0 ? filter : undefined)
  },
})

const allEventos = computed<AuditEventoDto[]>(() => eventos.value ?? [])

interface EventoRow {
  id: number
  occurredAt: string
  actorSub: string
  actorRole: string
  source: string
  entity: string
  entityId: string | null
  operation: string
}

const tableRows = computed<EventoRow[]>(() =>
  allEventos.value.map((e) => ({
    id: e.id,
    occurredAt: e.occurredAt,
    actorSub: e.actorSub,
    actorRole: e.actorRole,
    source: e.source,
    entity: e.entity,
    entityId: e.entityId ?? '—',
    operation: e.operation,
  })),
)

const columns: Column<EventoRow>[] = [
  { key: 'occurredAt', label: 'Data/Hora', sortable: false },
  { key: 'entity', label: 'Entidade', sortable: false },
  { key: 'operation', label: 'Operação', sortable: false },
  { key: 'actorRole', label: 'Função', sortable: false },
  { key: 'source', label: 'Origem', sortable: false },
  { key: 'actorSub', label: 'Usuário', sortable: false },
]

function operationBadgeVariant(operation: string): OperationBadgeVariant {
  switch (operation) {
    case 'CREATE':
      return 'success'
    case 'UPDATE':
      return 'info'
    case 'DELETE':
      return 'danger'
    default:
      return 'default'
  }
}

function sourceBadgeVariant(source: string): SourceBadgeVariant {
  switch (source) {
    case 'rest':
      return 'default'
    case 'mcp':
      return 'info'
    case 'a2a':
      return 'warning'
    case 'job':
      return 'success'
    default:
      return 'default'
  }
}

const isEmpty = computed(() => !isLoading.value && tableRows.value.length === 0)
</script>

<template>
  <PageLayout max-width="full">
    <template #header>
      <PageHeader title="Auditoria" />
    </template>

    <!-- Toolbar -->
    <div class="auditoria-toolbar">
      <div class="auditoria-toolbar__search">
        <input
          v-model="entityFilter"
          type="text"
          class="auditoria-toolbar__input"
          placeholder="Filtrar por entidade..."
          aria-label="Filtrar por entidade"
        />
      </div>

      <div class="auditoria-toolbar__filters">
        <select v-model="operationFilter" class="auditoria-toolbar__select" aria-label="Filtrar por operação">
          <option value="">Todas as operações</option>
          <option value="CREATE">Criar</option>
          <option value="UPDATE">Atualizar</option>
          <option value="DELETE">Deletar</option>
        </select>

        <select v-model="sourceFilter" class="auditoria-toolbar__select" aria-label="Filtrar por origem">
          <option value="">Todas as origens</option>
          <option value="rest">REST</option>
          <option value="mcp">MCP</option>
          <option value="a2a">A2A</option>
          <option value="job">Job</option>
        </select>

        <input
          v-model="actorSubFilter"
          type="text"
          class="auditoria-toolbar__input"
          placeholder="Filtrar por usuário..."
          aria-label="Filtrar por usuário"
        />
      </div>
    </div>

    <!-- Loading -->
    <template v-if="isLoading && allEventos.length === 0">
      <div class="auditoria-skeletons">
        <Skeleton v-for="n in 6" :key="n" height="48px" />
      </div>
    </template>

    <!-- Error -->
    <Alert v-else-if="isError" variant="error" title="Erro ao carregar auditoria">
      {{ extractApiError(queryError) }}
    </Alert>

    <!-- Empty -->
    <EmptyState
      v-else-if="isEmpty"
      title="Nenhum evento encontrado"
      description="Ajuste os filtros para visualizar eventos de auditoria."
      icon="i-carbon-events"
    />

    <!-- Table -->
    <template v-else>
      <Card>
        <DataTable
          :columns="columns"
          :rows="tableRows"
          :loading="isLoading"
        >
          <template #cell-occurredAt="{ row }">
            <span class="evento-datetime">{{ (row as EventoRow).occurredAt }}</span>
          </template>

          <template #cell-operation="{ row }">
            <Badge
              :variant="operationBadgeVariant((row as EventoRow).operation)"
              size="sm"
            >
              {{ (row as EventoRow).operation }}
            </Badge>
          </template>

          <template #cell-source="{ row }">
            <Badge
              :variant="sourceBadgeVariant((row as EventoRow).source)"
              size="sm"
            >
              {{ (row as EventoRow).source.toUpperCase() }}
            </Badge>
          </template>

          <template #cell-actorSub="{ row }">
            <span class="evento-userid">{{ (row as EventoRow).actorSub }}</span>
          </template>
        </DataTable>
      </Card>
    </template>
  </PageLayout>
</template>

<style scoped>
.auditoria-toolbar {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
  background: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
}

.auditoria-toolbar__search {
  display: flex;
  flex: 1;
}

.auditoria-toolbar__filters {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.auditoria-toolbar__input,
.auditoria-toolbar__select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  font-size: 0.875rem;
  outline: none;
  background: var(--color-surface);
  color: var(--color-text-primary);
  transition: border-color 0.15s;
}

.auditoria-toolbar__input {
  width: 280px;
}

.auditoria-toolbar__select {
  min-width: 150px;
}

.auditoria-toolbar__input:focus,
.auditoria-toolbar__select:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(0, 249, 184, 0.1);
}

.auditoria-skeletons {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 1.5rem;
}

.evento-datetime {
  font-family: 'Courier New', monospace;
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
}

.evento-userid {
  font-family: 'Courier New', monospace;
  font-size: 0.8125rem;
  color: var(--color-text-secondary);
  word-break: break-all;
}
</style>

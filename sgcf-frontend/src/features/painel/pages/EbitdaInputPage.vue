<script setup lang="ts">
import { ref, reactive } from 'vue'
import {
  Button,
  Input,
  Select,
  Card,
  Alert,
} from '@nordware/design-system'
import { apiClient, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import { toast } from '@/shared/ui/toast'

// Local alias — SelectOption is not re-exported from the DS root index
interface SelectOption {
  label: string
  value: string | number
}

// ============================================================================
// State
// ============================================================================

const isSubmitting = ref(false)
const serverError = ref<string | null>(null)

const form = reactive({
  ano: new Date().getFullYear(),
  mes: new Date().getMonth() + 1,
  valorBrl: 0,
})

// ============================================================================
// Month options (1–12 in Portuguese)
// ============================================================================

const mesOptions: SelectOption[] = [
  { value: 1,  label: 'Janeiro' },
  { value: 2,  label: 'Fevereiro' },
  { value: 3,  label: 'Março' },
  { value: 4,  label: 'Abril' },
  { value: 5,  label: 'Maio' },
  { value: 6,  label: 'Junho' },
  { value: 7,  label: 'Julho' },
  { value: 8,  label: 'Agosto' },
  { value: 9,  label: 'Setembro' },
  { value: 10, label: 'Outubro' },
  { value: 11, label: 'Novembro' },
  { value: 12, label: 'Dezembro' },
]

// ============================================================================
// Submit
// ============================================================================

async function submit() {
  serverError.value = null
  isSubmitting.value = true
  try {
    await apiClient.post(API.painel.ebitda, {
      ano: form.ano,
      mes: form.mes,
      valorBrl: form.valorBrl,
    })
    toast.success('EBITDA atualizado com sucesso!')
    // Reset valor only
    form.valorBrl = 0
  } catch (err: unknown) {
    const msg = extractApiError(err)
    serverError.value = msg
    toast.error(msg)
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="ebitda-page">
    <div class="page-header">
      <h1 class="page-title">EBITDA Mensal</h1>
      <p class="page-subtitle">Informe o valor de EBITDA para o período selecionado.</p>
    </div>

    <Card title="Inserir / Atualizar EBITDA">
      <form class="ebitda-form" novalidate @submit.prevent="submit">
        <Alert v-if="serverError" variant="error" class="form-alert">
          {{ serverError }}
        </Alert>

        <div class="form-grid">
          <!-- Ano -->
          <div class="field">
            <Input
              :model-value="form.ano"
              label="Ano"
              type="number"
              placeholder="Ex: 2025"
              full-width
              @update:model-value="(v) => (form.ano = Number(v))"
            />
          </div>

          <!-- Mês -->
          <div class="field">
            <Select
              v-model="form.mes"
              label="Mês"
              :options="mesOptions"
              placeholder="Selecione o mês"
            />
          </div>

          <!-- Valor BRL -->
          <div class="field field--full">
            <Input
              :model-value="form.valorBrl"
              label="Valor BRL"
              type="number"
              placeholder="0.00"
              full-width
              @update:model-value="(v) => (form.valorBrl = Number(v))"
            />
          </div>
        </div>

        <div class="form-actions">
          <Button
            type="submit"
            variant="primary"
            :loading="isSubmitting"
            :disabled="isSubmitting"
          >
            Salvar EBITDA
          </Button>
        </div>
      </form>
    </Card>
  </div>
</template>

<style scoped>
.ebitda-page {
  max-width: 600px;
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

.ebitda-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-alert {
  margin-bottom: 0.25rem;
}

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

.form-actions {
  display: flex;
  justify-content: flex-end;
  padding-top: 0.5rem;
}

@media (max-width: 480px) {
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>

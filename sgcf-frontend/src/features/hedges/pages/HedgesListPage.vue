<script setup lang="ts">
import { useRouter } from 'vue-router'
import {
  PageLayout,
  PageHeader,
  Button,
  Alert,
} from '@nordware/design-system'

const router = useRouter()
</script>

<template>
  <PageLayout max-width="narrow">
    <template #header>
      <PageHeader title="Hedges" />
    </template>

    <div class="hedges-info">
      <Alert variant="info" title="Hedges gerenciados por contrato">
        Os hedges são gerenciados por contrato. Acesse a aba <strong>Hedges</strong> na
        página de detalhe de um contrato para visualizar, adicionar ou remover operações
        de hedge vinculadas.
      </Alert>

      <div class="hedges-info__action">
        <Button
          variant="primary"
          size="md"
          icon-left="i-carbon-document"
          @click="() => void router.push('/contratos')"
        >
          Ver Contratos
        </Button>
      </div>
    </div>
  </PageLayout>
</template>

<style scoped>
.hedges-info {
  padding: 2rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  max-width: 640px;
}

.hedges-info__action {
  display: flex;
}
</style>

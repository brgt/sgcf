<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'

const error = ref<Error | null>(null)
const errorInfo = ref<string>('')

onErrorCaptured((err, _instance, info) => {
  error.value = err instanceof Error ? err : new Error(String(err))
  errorInfo.value = info
  console.error('[ErrorBoundary]', err, info)
  return false // prevent propagation
})

function retry(): void {
  error.value = null
  errorInfo.value = ''
}
</script>

<template>
  <div v-if="error" style="padding:24px; text-align:center;">
    <h2 style="color:#e74c3c; margin-bottom:8px;">Algo deu errado</h2>
    <p style="color:#666; margin-bottom:16px;">{{ error.message }}</p>
    <button @click="retry" style="padding:8px 16px; cursor:pointer; border-radius:4px;">
      Tentar novamente
    </button>
    <details v-if="errorInfo" style="margin-top:16px; text-align:left;">
      <summary style="cursor:pointer; color:#999;">Detalhes técnicos</summary>
      <pre style="font-size:11px; color:#999;">{{ errorInfo }}</pre>
    </details>
  </div>
  <slot v-else />
</template>

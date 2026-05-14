<script setup lang="ts">
import { ref } from 'vue'
import { Modal, Button } from '@nordware/design-system'

interface ConfirmOptions {
  title?: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  variant?: 'danger' | 'default'
}

const isOpen = ref(false)
const options = ref<ConfirmOptions>({ message: '' })
let resolveFn: ((value: boolean) => void) | null = null

function confirm(opts: ConfirmOptions): Promise<boolean> {
  options.value = opts
  isOpen.value = true
  return new Promise<boolean>((resolve) => {
    resolveFn = resolve
  })
}

function handleConfirm(): void {
  isOpen.value = false
  resolveFn?.(true)
  resolveFn = null
}

function handleCancel(): void {
  isOpen.value = false
  resolveFn?.(false)
  resolveFn = null
}

defineExpose({ confirm })
</script>

<template>
  <Modal v-model="isOpen" :title="options.title ?? 'Confirmar'" size="sm">
    <p>{{ options.message }}</p>
    <template #footer>
      <div style="display:flex; gap:8px; justify-content:flex-end;">
        <Button variant="ghost" @click="handleCancel">
          {{ options.cancelLabel ?? 'Cancelar' }}
        </Button>
        <Button
          :variant="options.variant === 'danger' ? 'danger' : 'secondary'"
          @click="handleConfirm"
        >
          {{ options.confirmLabel ?? 'Confirmar' }}
        </Button>
      </div>
    </template>
  </Modal>
</template>

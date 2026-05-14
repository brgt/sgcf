<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RouterView } from 'vue-router'
import { Toast } from '@nordware/design-system'
import ConfirmDialog from '@/shared/ui/ConfirmDialog.vue'
import { useToasts } from '@/shared/ui/toast'
import { provideConfirm } from '@/shared/ui/useConfirm'

const { toasts } = useToasts()

const confirmDialogRef = ref<InstanceType<typeof ConfirmDialog> | null>(null)

onMounted(() => {
  if (confirmDialogRef.value) {
    provideConfirm(confirmDialogRef.value.confirm)
  }
})
</script>

<template>
  <div class="dark-theme">
    <RouterView />

    <!-- Global toast queue rendered via DS Toast atom -->
    <Teleport to="body">
      <div class="app-toast-container" aria-live="polite" aria-atomic="false">
        <TransitionGroup name="toast-list">
          <Toast
            v-for="t in toasts"
            :key="t.id"
            :id="t.id"
            :title="t.message"
            :variant="t.variant"
            :duration="t.duration"
            position="top-right"
            @close="() => {}"
          />
        </TransitionGroup>
      </div>
    </Teleport>

    <!-- Global confirm dialog -->
    <ConfirmDialog ref="confirmDialogRef" />
  </div>
</template>

<style>
.app-toast-container {
  position: fixed;
  top: 1.5rem;
  right: 1.5rem;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  pointer-events: none;
  max-width: 420px;
}

.toast-list-move,
.toast-list-enter-active,
.toast-list-leave-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.toast-list-enter-from {
  opacity: 0;
  transform: translateX(100%);
}

.toast-list-leave-to {
  opacity: 0;
  transform: translateX(100%);
}

.toast-list-leave-active {
  position: absolute;
}
</style>

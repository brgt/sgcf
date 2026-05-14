import { ref } from 'vue'

export type ToastVariant = 'success' | 'error' | 'warning' | 'info'

export interface ToastItem {
  id: string
  message: string
  variant: ToastVariant
  duration: number
}

// Module-level singleton — avoids Pinia dependency in tests
const toasts = ref<ToastItem[]>([])

function show(message: string, variant: ToastVariant, duration = 4000): void {
  const id = crypto.randomUUID()
  toasts.value.push({ id, message, variant, duration })
  setTimeout(() => {
    toasts.value = toasts.value.filter(t => t.id !== id)
  }, duration)
}

export const toast = {
  success: (message: string) => show(message, 'success'),
  error:   (message: string) => show(message, 'error', 6000),
  warning: (message: string) => show(message, 'warning'),
  info:    (message: string) => show(message, 'info'),
}

export function useToasts() {
  return { toasts }
}

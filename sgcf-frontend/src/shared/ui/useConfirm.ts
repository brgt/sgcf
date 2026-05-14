import { ref } from 'vue'

type ConfirmFn = (opts: {
  title?: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  variant?: 'danger' | 'default'
}) => Promise<boolean>

// Will be set by the component when mounted
const _confirm = ref<ConfirmFn | null>(null)

export function provideConfirm(fn: ConfirmFn): void {
  _confirm.value = fn
}

export function useConfirm(): ConfirmFn {
  return (opts) => {
    if (!_confirm.value) {
      console.warn('[useConfirm] ConfirmDialog not mounted. Falling back to window.confirm.')
      return Promise.resolve(window.confirm(opts.message))
    }
    return _confirm.value(opts)
  }
}

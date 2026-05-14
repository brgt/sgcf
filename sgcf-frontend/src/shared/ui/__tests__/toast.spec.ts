import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

describe('toast', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('adds a success toast and it appears in toasts list', async () => {
    const { toast, useToasts } = await import('../toast')
    const { toasts } = useToasts()
    const countBefore = toasts.value.length
    toast.success('Operação concluída')
    expect(toasts.value.length).toBe(countBefore + 1)
    expect(toasts.value.some(t => t.message === 'Operação concluída' && t.variant === 'success')).toBe(true)
  })

  it('adds an error toast with 6000ms duration', async () => {
    const { toast, useToasts } = await import('../toast')
    const { toasts } = useToasts()
    toast.error('Falha na requisição')
    const errToast = toasts.value.find(t => t.message === 'Falha na requisição')
    expect(errToast).toBeDefined()
    expect(errToast?.variant).toBe('error')
    expect(errToast?.duration).toBe(6000)
  })

  it('removes toast after its duration elapses', async () => {
    const { toast, useToasts } = await import('../toast')
    const { toasts } = useToasts()
    const uniqueMsg = 'Temp-' + Date.now()
    toast.success(uniqueMsg)
    expect(toasts.value.some(t => t.message === uniqueMsg)).toBe(true)
    vi.advanceTimersByTime(5000)
    expect(toasts.value.some(t => t.message === uniqueMsg)).toBe(false)
  })

  it('adds a warning toast', async () => {
    const { toast, useToasts } = await import('../toast')
    const { toasts } = useToasts()
    toast.warning('Atenção')
    expect(toasts.value.some(t => t.message === 'Atenção' && t.variant === 'warning')).toBe(true)
  })

  it('adds an info toast', async () => {
    const { toast, useToasts } = await import('../toast')
    const { toasts } = useToasts()
    toast.info('Informação')
    expect(toasts.value.some(t => t.message === 'Informação' && t.variant === 'info')).toBe(true)
  })

  it('keeps other toasts when one expires', async () => {
    const { toast, useToasts } = await import('../toast')
    const { toasts } = useToasts()
    const msg1 = 'First-' + Date.now()
    const msg2 = 'Second-' + Date.now()
    toast.success(msg1)
    vi.advanceTimersByTime(2000)
    toast.success(msg2)
    vi.advanceTimersByTime(2500) // msg1 expires at 4000ms total, msg2 still alive
    expect(toasts.value.some(t => t.message === msg1)).toBe(false)
    expect(toasts.value.some(t => t.message === msg2)).toBe(true)
  })
})

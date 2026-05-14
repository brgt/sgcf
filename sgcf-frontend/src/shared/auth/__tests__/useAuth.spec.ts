import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

describe('useAuth — authenticated with tesouraria + admin roles', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubEnv('VITE_DEV_TOKEN', 'mock-token')
    vi.stubEnv('VITE_DEV_ROLES', 'tesouraria,admin')
    setActivePinia(createPinia())
  })

  it('should be authenticated when VITE_DEV_TOKEN is set', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(true)
  })

  it('should parse roles from VITE_DEV_ROLES', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.user?.roles).toContain('tesouraria')
    expect(auth.user?.roles).toContain('admin')
  })
})

describe('Policy checks — tesouraria role only', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubEnv('VITE_DEV_TOKEN', 'mock-token')
    vi.stubEnv('VITE_DEV_ROLES', 'tesouraria')
    setActivePinia(createPinia())
  })

  it('Leitura: any authenticated user passes', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.hasPolicy('Leitura')).toBe(true)
  })

  it('Escrita: tesouraria passes', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.hasPolicy('Escrita')).toBe(true)
  })

  it('Admin: tesouraria fails', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.hasPolicy('Admin')).toBe(false)
  })

  it('Gerencial: tesouraria fails', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.hasPolicy('Gerencial')).toBe(false)
  })

  it('Executivo: tesouraria passes', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.hasPolicy('Executivo')).toBe(true)
  })

  it('hasRole: known role returns true, unknown role returns false', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.hasRole('tesouraria')).toBe(true)
    expect(auth.hasRole('admin')).toBe(false)
  })
})

describe('Policy checks — unauthenticated (no token)', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubEnv('VITE_DEV_TOKEN', '')
    vi.stubEnv('VITE_DEV_ROLES', '')
    setActivePinia(createPinia())
  })

  it('isAuthenticated is false when no token', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('hasPolicy returns false for any policy when unauthenticated', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.hasPolicy('Leitura')).toBe(false)
    expect(auth.hasPolicy('Admin')).toBe(false)
  })

  it('user is null when no token', async () => {
    const { useAuthStore } = await import('../useAuth')
    const auth = useAuthStore()
    expect(auth.user).toBeNull()
  })
})

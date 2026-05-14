import { describe, it, expect } from 'vitest'

describe('usePagedList', () => {
  it('should be importable and export usePagedList', async () => {
    const mod = await import('../usePagedList')
    expect(mod.usePagedList).toBeDefined()
    expect(typeof mod.usePagedList).toBe('function')
  })
})

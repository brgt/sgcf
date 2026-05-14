import { describe, it, expect, vi, beforeEach } from 'vitest'
import { reactive } from 'vue'

// ----------------------------------------------------------------
// Mutable mock route so individual tests can control query params
// ----------------------------------------------------------------
const mockQuery = reactive<Record<string, string | string[] | null | undefined>>({})
const mockRouterReplace = vi.fn()
const mockRouterPush = vi.fn()

vi.mock('vue-router', () => ({
  useRoute: () => ({ query: mockQuery }),
  useRouter: () => ({
    push: mockRouterPush,
    replace: mockRouterReplace,
  }),
}))

describe('useUrlFilters', () => {
  beforeEach(() => {
    // Clear query and spy call history between tests
    for (const key in mockQuery) {
      delete mockQuery[key]
    }
    mockRouterReplace.mockClear()
    mockRouterPush.mockClear()
  })

  it('returns default values when URL has no query params', async () => {
    const { useUrlFilters } = await import('../useUrlFilters')
    const defaults = { search: '', page: 1, pageSize: 25 }
    const { filters } = useUrlFilters(defaults)
    expect(filters.value.search).toBe('')
    expect(filters.value.page).toBe(1)
    expect(filters.value.pageSize).toBe(25)
  })

  it('reads string values from URL query', async () => {
    mockQuery['search'] = 'ABC123'
    const { useUrlFilters } = await import('../useUrlFilters')
    const { filters } = useUrlFilters({ search: '', page: 1 })
    expect(filters.value.search).toBe('ABC123')
  })

  it('coerces numeric query params from string', async () => {
    mockQuery['page'] = '3'
    const { useUrlFilters } = await import('../useUrlFilters')
    const { filters } = useUrlFilters({ search: '', page: 1 })
    expect(filters.value.page).toBe(3)
  })

  it('falls back to default for non-numeric value in numeric field', async () => {
    mockQuery['page'] = 'abc'
    const { useUrlFilters } = await import('../useUrlFilters')
    const { filters } = useUrlFilters({ search: '', page: 1 })
    expect(filters.value.page).toBe(1)
  })

  it('coerces boolean query params', async () => {
    mockQuery['active'] = 'true'
    const { useUrlFilters } = await import('../useUrlFilters')
    const { filters } = useUrlFilters({ active: false })
    expect(filters.value.active).toBe(true)
  })

  it('setFilter calls router.replace with merged query', async () => {
    const { useUrlFilters } = await import('../useUrlFilters')
    const { setFilter } = useUrlFilters({ search: '', page: 1 })
    setFilter('search', 'hello')
    expect(mockRouterReplace).toHaveBeenCalledWith({ query: { search: 'hello' } })
  })

  it('setFilter resets page to default when non-page key changes', async () => {
    mockQuery['page'] = '5'
    const { useUrlFilters } = await import('../useUrlFilters')
    const { setFilter } = useUrlFilters({ search: '', page: 1 })
    setFilter('search', 'test')
    // page resets to default (1), which equals the default so it is omitted from query
    expect(mockRouterReplace).toHaveBeenCalledWith({ query: { search: 'test' } })
  })

  it('resetFilters calls router.replace with empty query', async () => {
    const { useUrlFilters } = await import('../useUrlFilters')
    const { resetFilters } = useUrlFilters({ search: '', page: 1 })
    resetFilters()
    expect(mockRouterReplace).toHaveBeenCalledWith({ query: {} })
  })
})

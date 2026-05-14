import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useRoute, useRouter } from 'vue-router'

type Primitive = string | number | boolean | null | undefined

/**
 * Omit keys from an object where the value equals the default value.
 * Used to keep the URL clean by not writing default values.
 */
function omitDefaults<T extends Record<string, Primitive>>(
  obj: T,
  defaults: T,
): Record<string, string> {
  const result: Record<string, string> = {}
  for (const key in obj) {
    const val = obj[key]
    const def = defaults[key]
    if (val !== def && val !== null && val !== undefined && val !== '') {
      result[key] = String(val)
    }
  }
  return result
}

/**
 * Coerce a URL query value to the expected type based on the default value type.
 */
function coerce<T extends Primitive>(raw: string | string[] | undefined, defaultVal: T): T {
  if (raw === undefined || raw === null || Array.isArray(raw)) return defaultVal
  if (typeof defaultVal === 'number') {
    const n = Number(raw)
    return (isNaN(n) ? defaultVal : n) as T
  }
  if (typeof defaultVal === 'boolean') {
    return (raw === 'true' ? true : raw === 'false' ? false : defaultVal) as T
  }
  return (raw ?? defaultVal) as T
}

export interface UseUrlFiltersReturn<TFilters extends Record<string, Primitive>> {
  filters: ComputedRef<TFilters>
  setFilter: <K extends keyof TFilters>(key: K, value: TFilters[K], mode?: 'push' | 'replace') => void
  setFilters: (patch: Partial<TFilters>, mode?: 'push' | 'replace') => void
  resetFilters: () => void
}

/**
 * Composable that syncs a typed filter object with the URL query string.
 *
 * Usage:
 *   const { filters, setFilter, setFilters, resetFilters } = useUrlFilters(defaults)
 *
 *   // filters is a readonly computed ref
 *   const currentPage = filters.value.page
 *
 *   // Update a single filter (URL replace)
 *   setFilter('search', 'ABC123')
 *
 *   // Update multiple filters at once (URL push)
 *   setFilters({ bancoId: 'uuid', modalidade: 'Finimp' }, 'push')
 */
export function useUrlFilters<TFilters extends Record<string, Primitive>>(
  defaults: TFilters,
): UseUrlFiltersReturn<TFilters> {
  const route = useRoute()
  const router = useRouter()

  const filters = computed<TFilters>(() => {
    const result = { ...defaults }
    for (const key in defaults) {
      const raw = route.query[key]
      result[key] = coerce(
        Array.isArray(raw) ? raw[0] ?? undefined : raw ?? undefined,
        defaults[key] as Primitive,
      ) as TFilters[typeof key]
    }
    return result
  })

  function setFilters(patch: Partial<TFilters>, mode: 'push' | 'replace' = 'replace'): void {
    const next = { ...filters.value, ...patch }
    // Reset page to 1 when any non-page filter changes
    const changedNonPage = Object.keys(patch).some(k => k !== 'page')
    if (changedNonPage && 'page' in defaults) {
      (next as Record<string, Primitive>)['page'] = (defaults as Record<string, Primitive>)['page'] ?? 1
    }
    const query = omitDefaults(next, defaults)
    if (mode === 'push') {
      void router.push({ query })
    } else {
      void router.replace({ query })
    }
  }

  function setFilter<K extends keyof TFilters>(
    key: K,
    value: TFilters[K],
    mode: 'push' | 'replace' = 'replace',
  ): void {
    setFilters({ [key]: value } as unknown as Partial<TFilters>, mode)
  }

  function resetFilters(): void {
    void router.replace({ query: {} })
  }

  return { filters, setFilter, setFilters, resetFilters }
}

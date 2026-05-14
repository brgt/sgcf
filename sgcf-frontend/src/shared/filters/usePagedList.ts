import { computed, toValue } from 'vue'
import type { MaybeRefOrGetter } from 'vue'
import { useQuery, keepPreviousData } from '@tanstack/vue-query'
import type { UseQueryOptions } from '@tanstack/vue-query'

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface UsePagedListOptions<TFilters, TItem> {
  /** TanStack Query key prefix */
  queryKey: readonly unknown[]
  /** Function that calls the API. Receives current filter value. */
  fetchFn: (filters: TFilters) => Promise<PagedResult<TItem>>
  /** Reactive filter object (ref, computed, or getter) */
  filters: MaybeRefOrGetter<TFilters>
  /** Extra TanStack Query options */
  queryOptions?: Partial<UseQueryOptions<PagedResult<TItem>>>
}

/**
 * Composable that combines URL-synced filters with TanStack Query for server-paged lists.
 * - Uses keepPreviousData for smooth page transitions (no table flash)
 * - Query key includes filter state so changing filters auto-refetches
 */
export function usePagedList<TFilters, TItem>(
  opts: UsePagedListOptions<TFilters, TItem>,
) {
  const filtersRef = computed(() => toValue(opts.filters))

  const query = useQuery({
    queryKey: computed(() => [...opts.queryKey, filtersRef.value] as const),
    queryFn: () => opts.fetchFn(filtersRef.value),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
    ...opts.queryOptions,
  })

  const items = computed(() => query.data.value?.items ?? [])
  const total = computed(() => query.data.value?.total ?? 0)
  const currentPage = computed(() => query.data.value?.page ?? 1)
  const pageSize = computed(() => query.data.value?.pageSize ?? 25)
  const totalPages = computed(() => Math.ceil(total.value / pageSize.value))
  const isEmpty = computed(() => !query.isFetching.value && items.value.length === 0)

  return {
    ...query,
    items,
    total,
    currentPage,
    pageSize,
    totalPages,
    isEmpty,
  }
}

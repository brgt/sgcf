import { useQuery } from '@tanstack/vue-query'
import { apiClient } from '@/shared/api/client'
import type { BancoDto, PagedResult } from '@/shared/api/types'

/**
 * Fetches all bancos (up to 200) for use in dropdowns and filter panels.
 * Stale for 5 minutes — banco list rarely changes.
 */
export function useBancosOptions() {
  return useQuery({
    queryKey: ['bancos', 'all'] as const,
    queryFn: async (): Promise<BancoDto[]> => {
      const { data } = await apiClient.get<PagedResult<BancoDto>>('/api/v1/bancos', {
        params: { pageSize: 200 },
      })
      return data.items
    },
    staleTime: 5 * 60 * 1000,
  })
}

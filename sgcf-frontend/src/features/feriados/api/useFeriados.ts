import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import type { FeriadoDto, CreateFeriadoRequest, PagedResult } from '@/shared/api/types'

// ============================================================================
// API functions
// ============================================================================

export async function listFeriados(): Promise<PagedResult<FeriadoDto>> {
  const { data } = await apiClient.get<PagedResult<FeriadoDto>>(API.feriados.list)
  return data
}

export async function createFeriado(payload: CreateFeriadoRequest): Promise<FeriadoDto> {
  const { data } = await apiClient.post<FeriadoDto>(API.feriados.create, payload)
  return data
}

export async function deleteFeriado(id: string): Promise<void> {
  await apiClient.delete(API.feriados.delete(id))
}

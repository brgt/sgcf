import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import type { LancamentoContabilDto, CreateLancamentoRequest } from '@/shared/api/types'

export async function listLancamentos(contaId: string): Promise<LancamentoContabilDto[]> {
  const { data } = await apiClient.get<LancamentoContabilDto[]>(API.planoContas.lancamentos(contaId))
  return data
}

export async function createLancamento(contaId: string, payload: CreateLancamentoRequest): Promise<LancamentoContabilDto> {
  const { data } = await apiClient.post<LancamentoContabilDto>(API.planoContas.lancamentos(contaId), payload)
  return data
}

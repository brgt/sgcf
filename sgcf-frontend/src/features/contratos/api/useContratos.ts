import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import type { ContratoDto, PagedResult } from '@/shared/api/types'

// ============================================================================
// Filter defaults + types
// ============================================================================

/**
 * Default filter values.
 * Declared without `as const` so TypeScript infers widened primitive types
 * (string, number) — required for useUrlFilters<TFilters extends Record<string, Primitive>>.
 * Boolean-ish flags use string ('', 'true', 'false') because useUrlFilters
 * only handles Primitive = string | number | boolean | null | undefined.
 */
export const CONTRATO_FILTER_DEFAULTS = {
  search: '' as string,
  bancoId: '' as string,
  modalidade: '' as string,
  moeda: '' as string,
  status: '' as string,
  dataVencimentoDe: '' as string,
  dataVencimentoAte: '' as string,
  valorPrincipalMin: 0 as number,
  valorPrincipalMax: 0 as number,
  temHedge: '' as string,         // '' | 'true' | 'false'
  temGarantia: '' as string,      // '' | 'true' | 'false'
  temAlertaVencimento: '' as string, // '' | 'true' | 'false'
  page: 1 as number,
  pageSize: 25 as number,
  sort: 'DataVencimento' as string,
  dir: 'asc' as string,
}

export type MutableContratoFilters = typeof CONTRATO_FILTER_DEFAULTS

// ============================================================================
// API functions
// ============================================================================

/**
 * Build an Axios params object from the filter state.
 * - Empty strings are omitted (not sent to the API)
 * - Zero-valued numeric filters (valorPrincipalMin/Max) are omitted
 * - Boolean string filters ('true'/'false'/'') are converted properly
 */
export async function listContratos(
  filters: MutableContratoFilters,
): Promise<PagedResult<ContratoDto>> {
  const params: Record<string, string | number | boolean> = {}

  if (filters.search) params['search'] = filters.search
  if (filters.bancoId) params['bancoId'] = filters.bancoId
  if (filters.modalidade) params['modalidade'] = filters.modalidade
  if (filters.moeda) params['moeda'] = filters.moeda
  if (filters.status) params['status'] = filters.status
  if (filters.dataVencimentoDe) params['dataVencimentoDe'] = filters.dataVencimentoDe
  if (filters.dataVencimentoAte) params['dataVencimentoAte'] = filters.dataVencimentoAte
  if (filters.valorPrincipalMin > 0) params['valorPrincipalMin'] = filters.valorPrincipalMin
  if (filters.valorPrincipalMax > 0) params['valorPrincipalMax'] = filters.valorPrincipalMax

  if (filters.temHedge === 'true') params['temHedge'] = true
  else if (filters.temHedge === 'false') params['temHedge'] = false

  if (filters.temGarantia === 'true') params['temGarantia'] = true
  else if (filters.temGarantia === 'false') params['temGarantia'] = false

  if (filters.temAlertaVencimento === 'true') params['temAlertaVencimento'] = true
  else if (filters.temAlertaVencimento === 'false') params['temAlertaVencimento'] = false

  params['page'] = filters.page
  params['pageSize'] = filters.pageSize
  params['sort'] = filters.sort
  params['dir'] = filters.dir

  const { data } = await apiClient.get<PagedResult<ContratoDto>>(API.contratos.list, { params })
  return data
}

export async function getContrato(id: string): Promise<ContratoDto> {
  const { data } = await apiClient.get<ContratoDto>(API.contratos.get(id))
  return data
}

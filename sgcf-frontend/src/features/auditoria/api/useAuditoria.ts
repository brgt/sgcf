import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import type { AuditEventoDto, AuditFilter } from '@/shared/api/types'

export async function listAuditEvento(filter?: AuditFilter): Promise<AuditEventoDto[]> {
  const params = new URLSearchParams()

  if (filter?.entity) params.append('entity', filter.entity)
  if (filter?.entityId) params.append('entityId', filter.entityId)
  if (filter?.operation) params.append('operation', filter.operation)
  if (filter?.source) params.append('source', filter.source)
  if (filter?.actorSub) params.append('actorSub', filter.actorSub)

  const queryString = params.toString()
  const url = queryString ? `${API.auditoria.eventos}?${queryString}` : API.auditoria.eventos

  const { data } = await apiClient.get<AuditEventoDto[]>(url)
  return data
}

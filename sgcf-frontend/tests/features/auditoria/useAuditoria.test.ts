import { describe, it, expect, vi, beforeEach } from 'vitest'
import { listAuditEvento } from '@/features/auditoria/api/useAuditoria'
import * as client from '@/shared/api/client'

vi.mock('@/shared/api/client')

const mockApiClient = vi.mocked(client.apiClient)

describe('useAuditoria', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('listAuditEvento', () => {
    it('fetches audit eventos with optional filters', async () => {
      const mockData = [
        {
          id: 1,
          occurredAt: '2025-05-14T10:00:00Z',
          actorSub: 'user-123',
          actorRole: 'Admin',
          source: 'rest' as const,
          entity: 'Contrato',
          entityId: 'contrato-1',
          operation: 'CREATE' as const,
          diffJson: null,
          requestId: 'req-123',
        },
      ]

      mockApiClient.get.mockResolvedValue({ data: mockData })

      const result = await listAuditEvento()

      expect(mockApiClient.get).toHaveBeenCalled()
      expect(result).toEqual(mockData)
    })

    it('fetches audit eventos with filters', async () => {
      const mockData = []
      mockApiClient.get.mockResolvedValue({ data: mockData })

      const filters = {
        entity: 'Contrato',
        operation: 'CREATE' as const,
      }

      const result = await listAuditEvento(filters)

      expect(mockApiClient.get).toHaveBeenCalled()
      expect(result).toEqual(mockData)
    })
  })
})

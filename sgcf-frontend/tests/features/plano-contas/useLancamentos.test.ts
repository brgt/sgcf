import { describe, it, expect, vi, beforeEach } from 'vitest'
import { listLancamentos, createLancamento } from '@/features/plano-contas/api/useLancamentos'
import * as client from '@/shared/api/client'

vi.mock('@/shared/api/client')

const mockApiClient = vi.mocked(client.apiClient)

describe('useLancamentos', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('listLancamentos', () => {
    it('fetches lancamentos for a given plano conta id', async () => {
      const contaId = 'conta-123'
      const mockData = [
        {
          id: 'lance-1',
          contratoId: 'contrato-1',
          planoContaId: contaId,
          data: '2025-05-14',
          origem: 'Manual',
          valor: 1000,
          moeda: 'BRL',
          descricao: 'Lançamento 1',
          createdAt: '2025-05-14T10:00:00Z',
        },
      ]

      mockApiClient.get.mockResolvedValue({ data: mockData })

      const result = await listLancamentos(contaId)

      expect(mockApiClient.get).toHaveBeenCalled()
      expect(result).toEqual(mockData)
    })
  })

  describe('createLancamento', () => {
    it('creates a new lancamento', async () => {
      const contaId = 'conta-123'
      const payload = {
        contratoId: 'contrato-1',
        data: '2025-05-14',
        origem: 'Manual',
        valorDecimal: 1000,
        moeda: 'Brl' as const,
        descricao: 'Novo lançamento',
      }

      const mockResponse = {
        id: 'lance-1',
        contratoId: payload.contratoId,
        planoContaId: contaId,
        data: payload.data,
        origem: payload.origem,
        valor: payload.valorDecimal,
        moeda: payload.moeda,
        descricao: payload.descricao,
        createdAt: '2025-05-14T10:00:00Z',
      }

      mockApiClient.post.mockResolvedValue({ data: mockResponse })

      const result = await createLancamento(contaId, payload)

      expect(mockApiClient.post).toHaveBeenCalled()
      expect(result).toEqual(mockResponse)
    })
  })
})

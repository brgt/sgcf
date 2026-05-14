import { describe, it, expect } from 'vitest'
import { API } from '../endpoints'

describe('API Endpoints', () => {
  it('has contratos.update endpoint for PATCH', () => {
    const endpoint = API.contratos.update('contrato-123')
    expect(endpoint).toBe('/api/v1/contratos/contrato-123')
  })

  it('has feriados endpoints', () => {
    expect(API.feriados.list).toBe('/api/v1/feriados')
    expect(API.feriados.create).toBe('/api/v1/feriados')
    expect(API.feriados.delete('feriado-123')).toBe('/api/v1/feriados/feriado-123')
  })

  it('has lancamentos endpoint under planoContas', () => {
    const endpoint = API.planoContas.lancamentos('conta-123')
    expect(endpoint).toBe('/api/v1/plano-contas/conta-123/lancamentos')
  })

  it('has auditoria.eventos endpoint', () => {
    expect(API.auditoria.eventos).toBe('/audit/eventos')
  })
})

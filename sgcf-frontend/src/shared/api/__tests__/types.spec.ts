import { describe, it, expect } from 'vitest'
import type {
  ContratoDto,
  UpdateContratoRequest,
  FeriadoDto,
  CreateFeriadoRequest,
  LancamentoContabilDto,
  AuditEventoDto,
  PagedResult,
} from '../types'
import {
  Periodicidade,
  EstruturaAmortizacao,
  AnchorDiaMes,
  ConvencaoDataNaoUtil,
  EscopoFeriado,
  TipoFeriado,
  FonteFeriado,
} from '../enums'

describe('Types', () => {
  it('ContratoDto includes all Sprint 3 amortization fields', () => {
    // This is a compile-time test — it checks that the type is defined correctly
    const contrato: ContratoDto = {
      id: 'test',
      numeroExterno: '123',
      codigoInterno: null,
      bancoId: 'banco-1',
      modalidade: 'Finimp' as any,
      moeda: 'Brl' as any,
      valorPrincipal: 1000,
      dataContratacao: '2025-01-01',
      dataVencimento: '2026-01-01',
      taxaAa: 5.5,
      baseCalculo: 'Du252' as any,
      periodicidade: Periodicidade.Mensal,
      estruturaAmortizacao: EstruturaAmortizacao.Bullet,
      quantidadeParcelas: 12,
      dataPrimeiroVencimento: '2025-02-01',
      anchorDiaMes: AnchorDiaMes.DiaContratacao,
      anchorDiaFixo: null,
      periodicidadeJuros: null,
      convencaoDataNaoUtil: ConvencaoDataNaoUtil.Following,
      status: 'Ativo' as any,
      contratoPaiId: null,
      observacoes: null,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
      parcelas: [],
      garantias: [],
      hedges: [],
      finimpDetail: null,
      lei4131Detail: null,
      refinimpDetail: null,
      nceDetail: null,
      balcaoCaixaDetail: null,
      fgiDetail: null,
    }
    expect(contrato.periodicidade).toBe(Periodicidade.Mensal)
    expect(contrato.estruturaAmortizacao).toBe(EstruturaAmortizacao.Bullet)
  })

  it('UpdateContratoRequest allows partial updates', () => {
    const updateRequest: UpdateContratoRequest = {
      numeroExterno: '456',
      periodicidade: Periodicidade.Anual,
      convencaoDataNaoUtil: ConvencaoDataNaoUtil.Preceding,
    }
    expect(updateRequest.numeroExterno).toBe('456')
    expect(updateRequest.periodicidade).toBe(Periodicidade.Anual)
  })

  it('FeriadoDto defines holiday structure', () => {
    const feriado: FeriadoDto = {
      id: 'feriado-1',
      data: '2025-12-25',
      descricao: 'Natal',
      abrangencia: EscopoFeriado.Nacional,
      tipo: TipoFeriado.FixoCalendario,
      fonte: FonteFeriado.Manual,
      createdAt: '2025-01-01T00:00:00Z',
    }
    expect(feriado.abrangencia).toBe(EscopoFeriado.Nacional)
    expect(feriado.fonte).toBe(FonteFeriado.Manual)
  })

  it('CreateFeriadoRequest requires holiday creation fields', () => {
    const createFeriado: CreateFeriadoRequest = {
      data: '2025-12-25',
      descricao: 'Natal',
      abrangencia: EscopoFeriado.Nacional,
      tipo: TipoFeriado.FixoCalendario,
    }
    expect(createFeriado.data).toBe('2025-12-25')
  })

  it('LancamentoContabilDto defines accounting entry structure', () => {
    const lancamento: LancamentoContabilDto = {
      id: 'lancamento-1',
      contratoId: 'contrato-1',
      planoContaId: 'conta-1',
      data: '2025-01-15',
      origem: 'Sistema',
      valor: 5000.5,
      moeda: 'Brl' as any,
      descricao: 'Juros contratados',
      createdAt: '2025-01-01T00:00:00Z',
    }
    expect(lancamento.contratoId).toBe('contrato-1')
    expect(lancamento.valor).toBe(5000.5)
  })

  it('AuditEventoDto defines audit log structure', () => {
    const evento: AuditEventoDto = {
      id: 1,
      occurredAt: '2025-01-01T00:00:00Z',
      actorSub: 'user-123',
      actorRole: 'admin',
      source: 'rest',
      entity: 'Contrato',
      entityId: 'contrato-1',
      operation: 'CREATE',
      diffJson: null,
      requestId: 'req-123',
    }
    expect(evento.operation).toBe('CREATE')
    expect(evento.source).toBe('rest')
  })

  it('PagedResult is generic and works with any type', () => {
    const pagedFeriados: PagedResult<FeriadoDto> = {
      items: [],
      total: 0,
      page: 1,
      pageSize: 50,
    }
    expect(pagedFeriados.pageSize).toBe(50)
  })
})

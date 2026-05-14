import { describe, it, expect } from 'vitest'
import {
  Moeda,
  ModalidadeContrato,
  StatusContrato,
  TipoCotacao,
  Periodicidade,
  EstruturaAmortizacao,
  AnchorDiaMes,
  ConvencaoDataNaoUtil,
  EscopoFeriado,
  TipoFeriado,
  FonteFeriado,
} from '../enums'

describe('Enums', () => {
  it('Moeda has all 5 currencies', () => {
    expect(Object.keys(Moeda)).toHaveLength(5)
    expect(Moeda.Brl).toBe('Brl')
    expect(Moeda.Usd).toBe('Usd')
  })

  it('ModalidadeContrato has all 6 modalities', () => {
    expect(Object.keys(ModalidadeContrato)).toHaveLength(6)
    expect(ModalidadeContrato.Finimp).toBe('Finimp')
  })

  it('StatusContrato has 7 statuses', () => {
    expect(Object.keys(StatusContrato)).toHaveLength(7)
  })

  it('TipoCotacao has 3 types', () => {
    expect(Object.keys(TipoCotacao)).toHaveLength(3)
  })

  // New enums for Sprint 3
  it('Periodicidade has 6 values', () => {
    expect(Object.keys(Periodicidade)).toHaveLength(6)
    expect(Periodicidade.Bullet).toBe('Bullet')
    expect(Periodicidade.Mensal).toBe('Mensal')
  })

  it('EstruturaAmortizacao has 4 values', () => {
    expect(Object.keys(EstruturaAmortizacao)).toHaveLength(4)
    expect(EstruturaAmortizacao.Bullet).toBe('Bullet')
    expect(EstruturaAmortizacao.Price).toBe('Price')
  })

  it('AnchorDiaMes has 3 values', () => {
    expect(Object.keys(AnchorDiaMes)).toHaveLength(3)
    expect(AnchorDiaMes.DiaContratacao).toBe('DiaContratacao')
    expect(AnchorDiaMes.DiaFixo).toBe('DiaFixo')
  })

  it('ConvencaoDataNaoUtil has 4 values', () => {
    expect(Object.keys(ConvencaoDataNaoUtil)).toHaveLength(4)
    expect(ConvencaoDataNaoUtil.Following).toBe('Following')
    expect(ConvencaoDataNaoUtil.Preceding).toBe('Preceding')
  })

  it('EscopoFeriado has 3 values', () => {
    expect(Object.keys(EscopoFeriado)).toHaveLength(3)
    expect(EscopoFeriado.Nacional).toBe('Nacional')
    expect(EscopoFeriado.Estadual).toBe('Estadual')
  })

  it('TipoFeriado has 3 values', () => {
    expect(Object.keys(TipoFeriado)).toHaveLength(3)
    expect(TipoFeriado.FixoCalendario).toBe('FixoCalendario')
    expect(TipoFeriado.Pontual).toBe('Pontual')
  })

  it('FonteFeriado has 2 values', () => {
    expect(Object.keys(FonteFeriado)).toHaveLength(2)
    expect(FonteFeriado.Manual).toBe('Manual')
    expect(FonteFeriado.Anbima).toBe('Anbima')
  })
})

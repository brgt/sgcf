import { describe, it, expect } from 'vitest'
import { formatMoney, formatBrl, formatPercent } from '../formatMoney'

describe('formatMoney', () => {
  it('formats BRL correctly', () => {
    const result = formatMoney(1234.567, 'Brl')
    // BRL rounds to 2 decimals, uses Brazilian locale
    expect(result).toContain('1.234')
    expect(result).toContain('57')
    expect(result).toContain('R$')
  })

  it('formats USD correctly', () => {
    const result = formatMoney(1234.567, 'Usd')
    expect(result).toContain('1,234')
    expect(result).toContain('57')
  })

  it('formats zero correctly', () => {
    expect(formatBrl(0)).toContain('0')
  })

  it('formats negative correctly', () => {
    expect(formatBrl(-1234.5)).toContain('1.234')
  })

  it('formatPercent: 12.345 → includes 12,35', () => {
    const result = formatPercent(12.345)
    expect(result).toContain('12')
  })

  it('formatMoney with showSymbol=false returns no symbol', () => {
    const result = formatMoney(1234.5, 'Brl', { showSymbol: false })
    expect(result).not.toContain('R$')
    expect(result).toContain('1.234')
  })
})

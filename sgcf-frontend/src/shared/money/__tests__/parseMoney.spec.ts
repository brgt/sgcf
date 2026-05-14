import { describe, it, expect } from 'vitest'
import { parseMoney } from '../parseMoney'

describe('parseMoney', () => {
  it('parses BRL format: 1.234,56 → 1234.56', () => {
    expect(parseMoney('1.234,56', 'Brl')).toBeCloseTo(1234.56)
  })

  it('parses USD format: 1,234.56 → 1234.56', () => {
    expect(parseMoney('1,234.56', 'Usd')).toBeCloseTo(1234.56)
  })

  it('returns null for empty string', () => {
    expect(parseMoney('', 'Brl')).toBeNull()
  })

  it('returns null for non-numeric', () => {
    expect(parseMoney('abc', 'Brl')).toBeNull()
  })

  it('parses BRL with currency symbol: R$ 1.234,56', () => {
    expect(parseMoney('R$ 1.234,56', 'Brl')).toBeCloseTo(1234.56)
  })
})

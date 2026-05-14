import { describe, it, expect } from 'vitest'
import { formatInstant, formatInstantDate } from '../formatInstant'

describe('formatInstant', () => {
  it('formats UTC instant to BRT date+time', () => {
    // UTC 15:00 = BRT 12:00 (UTC-3)
    const result = formatInstant('2026-05-12T15:00:00Z')
    expect(result).toContain('12/05/2026')
    expect(result).toContain('12')
  })

  it('returns — for null', () => {
    expect(formatInstant(null)).toBe('—')
  })

  it('returns — for undefined', () => {
    expect(formatInstant(undefined)).toBe('—')
  })

  it('date-only variant excludes time', () => {
    const result = formatInstantDate('2026-05-12T15:00:00Z')
    expect(result).toContain('12/05/2026')
  })
})

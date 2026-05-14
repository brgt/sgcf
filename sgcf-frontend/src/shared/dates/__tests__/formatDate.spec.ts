import { describe, it, expect } from 'vitest'
import { formatLocalDate, formatLocalDateShort, parseDisplayDate, todayBrasilia } from '../formatDate'

describe('formatLocalDate', () => {
  it('formats yyyy-MM-dd to dd/MM/yyyy', () => {
    expect(formatLocalDate('2026-05-12')).toBe('12/05/2026')
  })

  it('returns — for null', () => {
    expect(formatLocalDate(null)).toBe('—')
  })

  it('returns — for undefined', () => {
    expect(formatLocalDate(undefined)).toBe('—')
  })

  it('short format returns month/year', () => {
    const result = formatLocalDateShort('2026-05-12')
    expect(result).toBe('mai/2026')
  })
})

describe('parseDisplayDate', () => {
  it('parses dd/MM/yyyy to yyyy-MM-dd', () => {
    expect(parseDisplayDate('12/05/2026')).toBe('2026-05-12')
  })

  it('returns null for invalid input', () => {
    expect(parseDisplayDate('')).toBeNull()
    expect(parseDisplayDate('invalid')).toBeNull()
  })
})

describe('todayBrasilia', () => {
  it('returns a valid yyyy-MM-dd string', () => {
    const today = todayBrasilia()
    expect(today).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })
})

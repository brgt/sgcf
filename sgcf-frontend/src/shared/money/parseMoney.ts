import type { MoedaCode } from './Money'

/**
 * Parse a localized monetary input string back to a number.
 * Handles BRL format (1.234,56), USD format (1,234.56), etc.
 * Returns null if the input cannot be parsed.
 *
 * IMPORTANT: Only use this at form submission, never during calculation.
 */
export function parseMoney(input: string, moeda: MoedaCode): number | null {
  if (!input || typeof input !== 'string') return null

  let normalized = input.trim()

  // Remove currency symbols and whitespace
  normalized = normalized.replace(/[R$€¥元\s]/g, '').trim()

  if (moeda === 'Brl' || moeda === 'Eur') {
    // BRL/EUR format: 1.234,56 → remove thousand dots, replace comma decimal
    normalized = normalized.replace(/\./g, '').replace(',', '.')
  } else {
    // USD/JPY/CNY format: 1,234.56 → remove thousand commas
    normalized = normalized.replace(/,/g, '')
  }

  const parsed = parseFloat(normalized)
  return isNaN(parsed) ? null : parsed
}

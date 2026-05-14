import type { MoedaCode } from './Money'

interface FormatMoneyOptions {
  /** Number of decimal places (default: 2) */
  decimals?: number
  /** Show currency symbol (default: true) */
  showSymbol?: boolean
}

const LOCALE_MAP: Record<MoedaCode, string> = {
  Brl: 'pt-BR',
  Usd: 'en-US',
  Eur: 'de-DE',
  Jpy: 'ja-JP',
  Cny: 'zh-CN',
}

const CURRENCY_MAP: Record<MoedaCode, string> = {
  Brl: 'BRL',
  Usd: 'USD',
  Eur: 'EUR',
  Jpy: 'JPY',
  Cny: 'CNY',
}

/**
 * Format a monetary amount for display.
 * BRL → "R$ 1.234,57", USD → "US$ 1,234.57", EUR → "1.234,57 €", JPY → "¥1,235"
 */
export function formatMoney(
  valor: number,
  moeda: MoedaCode,
  options: FormatMoneyOptions = {},
): string {
  const { decimals = 2, showSymbol = true } = options
  const locale = LOCALE_MAP[moeda]
  const currency = CURRENCY_MAP[moeda]

  const fmt = new Intl.NumberFormat(locale, {
    style: showSymbol ? 'currency' : 'decimal',
    currency: showSymbol ? currency : undefined,
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })

  return fmt.format(valor)
}

/**
 * Format a BRL amount with BRL locale (shorthand for the most common case).
 */
export function formatBrl(valor: number, decimals = 2): string {
  return formatMoney(valor, 'Brl', { decimals })
}

/**
 * Format a percentage with 2 decimal places and % symbol.
 * e.g. 12.345678 → "12,35%"
 */
export function formatPercent(valor: number, decimals = 2): string {
  return new Intl.NumberFormat('pt-BR', {
    style: 'percent',
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(valor / 100)
}

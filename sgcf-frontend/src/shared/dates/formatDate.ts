/**
 * Format a LocalDate string (yyyy-MM-dd) from the backend to Brazilian display format (dd/MM/yyyy).
 * No timezone conversion needed — LocalDate has no time component.
 */
export function formatLocalDate(isoDate: string | null | undefined): string {
  if (!isoDate) return '—'
  // Parse yyyy-MM-dd manually to avoid any timezone ambiguity with Date()
  const parts = isoDate.split('-')
  if (parts.length !== 3) return isoDate
  const [year, month, day] = parts
  return `${day}/${month}/${year}`
}

/**
 * Format as short month label: "mai/2026"
 */
export function formatLocalDateShort(isoDate: string | null | undefined): string {
  if (!isoDate) return '—'
  const parts = isoDate.split('-')
  if (parts.length < 2) return isoDate
  const [year, monthStr] = parts
  const month = parseInt(monthStr ?? '1', 10) - 1
  const monthNames = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']
  return `${monthNames[month] ?? '?'}/${year}`
}

/**
 * Parse display date (dd/MM/yyyy) to ISO date (yyyy-MM-dd) for API requests.
 */
export function parseDisplayDate(display: string): string | null {
  if (!display || display.length < 10) return null
  const parts = display.split('/')
  if (parts.length !== 3) return null
  const [day, month, year] = parts
  if (!day || !month || !year) return null
  return `${year}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`
}

/**
 * Get today's date as ISO string (yyyy-MM-dd) in Brazil timezone.
 * Uses Intl to get the current date in America/Sao_Paulo.
 */
export function todayBrasilia(): string {
  const fmt = new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Sao_Paulo' })
  return fmt.format(new Date())
}

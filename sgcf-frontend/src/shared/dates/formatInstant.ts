/**
 * Format an Instant string (ISO 8601 UTC, e.g. "2026-05-12T15:30:00Z")
 * to Brazilian display format in America/Sao_Paulo timezone.
 * Result: "12/05/2026 12:30" (BRT = UTC-3)
 */
export function formatInstant(
  isoInstant: string | null | undefined,
  opts?: { withTime?: boolean },
): string {
  if (!isoInstant) return '—'
  const { withTime = true } = opts ?? {}
  try {
    const date = new Date(isoInstant)
    if (isNaN(date.getTime())) return isoInstant

    const fmt = new Intl.DateTimeFormat('pt-BR', {
      timeZone: 'America/Sao_Paulo',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      ...(withTime
        ? { hour: '2-digit', minute: '2-digit', hour12: false }
        : {}),
    })
    return fmt.format(date)
  } catch {
    return isoInstant
  }
}

/**
 * Format an Instant to just the date part (no time) in BRT.
 */
export function formatInstantDate(isoInstant: string | null | undefined): string {
  return formatInstant(isoInstant, { withTime: false })
}

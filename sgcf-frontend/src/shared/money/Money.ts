// Mirrors the backend Money value object concept.
// On the wire: monetary amounts come as JS numbers from JSON.
// We handle them as numbers here (16-digit precision is sufficient for display).

export type MoedaCode = 'Brl' | 'Usd' | 'Eur' | 'Jpy' | 'Cny'

export interface Money {
  valor: number
  moeda: MoedaCode
}

/** Create a Money value */
export function money(valor: number, moeda: MoedaCode): Money {
  return { valor, moeda }
}

// Maps backend Policies.cs constants to their role requirements.
export const POLICY_ROLES: Record<string, string[]> = {
  Leitura:   [],   // any authenticated user — empty means "authenticated only"
  Escrita:   ['tesouraria', 'admin'],
  Gerencial: ['gerente', 'diretor', 'admin'],
  Executivo: ['tesouraria', 'gerente', 'diretor', 'admin'],
  Auditoria: ['contabilidade', 'auditor', 'admin'],
  Admin:     ['admin'],
}

export type Policy = keyof typeof POLICY_ROLES

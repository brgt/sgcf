import { computed } from 'vue'
import { defineStore } from 'pinia'
import { POLICY_ROLES } from './policies'

export interface AuthUser {
  id: string
  name: string
  email: string
  roles: string[]
}

export const useAuthStore = defineStore('auth', () => {
  // In v1: reads from .env for mock auth.
  // The VITE_DEV_ROLES env var is a comma-separated list of roles.
  const devToken = import.meta.env['VITE_DEV_TOKEN'] as string | undefined
  const devRolesRaw = import.meta.env['VITE_DEV_ROLES'] as string | undefined
  const devRoles = devRolesRaw ? devRolesRaw.split(',').map((r: string) => r.trim()).filter(Boolean) : []

  const isAuthenticated = computed(() => Boolean(devToken))

  const user = computed<AuthUser | null>(() => {
    if (!devToken) return null
    return {
      id: 'dev-user',
      name: 'Dev User',
      email: 'dev@sgcf.local',
      roles: devRoles,
    }
  })

  const token = computed(() => devToken ?? null)

  function hasPolicy(policy: string): boolean {
    if (!isAuthenticated.value) return false
    const requiredRoles = POLICY_ROLES[policy]
    if (!requiredRoles) return false
    // Empty array = any authenticated user
    if (requiredRoles.length === 0) return true
    const userRoles = user.value?.roles ?? []
    return requiredRoles.some(r => userRoles.includes(r))
  }

  function hasRole(role: string): boolean {
    return user.value?.roles.includes(role) ?? false
  }

  // Placeholder for future OIDC integration
  function login() {
    console.warn('[Auth] Real auth not implemented. Using mock dev token.')
  }

  function logout() {
    console.warn('[Auth] Real logout not implemented.')
  }

  return { user, isAuthenticated, token, hasPolicy, hasRole, login, logout }
})

// Convenience composable (alias to store)
export const useAuth = useAuthStore

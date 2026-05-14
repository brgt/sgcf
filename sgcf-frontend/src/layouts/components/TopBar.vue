<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Button, Badge } from '@nordware/design-system'
import { useAuth } from '@/shared/auth/useAuth'

const route = useRoute()
const router = useRouter()
const auth = useAuth()

const pageTitle = computed<string>(() => {
  const metaTitle = route.meta['title']
  if (typeof metaTitle === 'string' && metaTitle.length > 0) return metaTitle

  // Derive a human-readable title from the last path segment
  const segments = route.path.replace(/^\//, '').split('/')
  const last = segments[segments.length - 1] ?? ''
  return last
    .replace(/-/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase()) || 'SGCF'
})

const userRoles = computed<string[]>(() => auth.user?.roles ?? [])

async function handleLogout(): Promise<void> {
  auth.logout()
  await router.push('/login')
}
</script>

<template>
  <header class="topbar" role="banner">
    <!-- Page title -->
    <div class="topbar__title">
      <h1 class="topbar__heading">{{ pageTitle }}</h1>
    </div>

    <!-- Right section: user identity + logout -->
    <div class="topbar__actions">
      <div v-if="auth.user" class="topbar__user" aria-label="Usuário autenticado">
        <span class="topbar__user-name">{{ auth.user.name }}</span>
        <div class="topbar__roles" aria-label="Perfis de acesso">
          <Badge
            v-for="role in userRoles"
            :key="role"
            variant="info"
            size="sm"
            pill
          >
            {{ role }}
          </Badge>
        </div>
      </div>

      <Button
        variant="ghost"
        size="sm"
        icon-left="i-carbon-logout"
        aria-label="Sair do sistema"
        @click="handleLogout"
      >
        Sair
      </Button>
    </div>
  </header>
</template>

<style scoped>
.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1.5rem;
  height: 3.5rem;
  flex-shrink: 0;
  background: var(--color-surface);
  border-bottom: 1px solid var(--color-border, #e5e7eb);
}

.topbar__title {
  flex: 1;
  min-width: 0;
}

.topbar__heading {
  font-size: 1rem;
  font-weight: 600;
  color: var(--color-text-primary, #111827);
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.topbar__actions {
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-shrink: 0;
}

.topbar__user {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.topbar__user-name {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--color-text-primary, #111827);
  white-space: nowrap;
}

.topbar__roles {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  flex-wrap: wrap;
}
</style>

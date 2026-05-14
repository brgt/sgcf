<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Button, Card } from '@nordware/design-system'
import { useAuth } from '@/shared/auth/useAuth'

const router = useRouter()
const auth = useAuth()

function handleLogin(): void {
  auth.login()
  void router.push('/painel')
}
</script>

<template>
  <div class="public-layout" role="main">
    <div class="public-layout__container">
      <!-- Branding -->
      <div class="public-layout__brand" aria-label="SGCF">
        <span class="public-layout__brand-icon i-mdi-bank-outline" aria-hidden="true" />
        <span class="public-layout__brand-text">SGCF</span>
        <p class="public-layout__brand-subtitle">
          Sistema de Gestão de Contratos de Financiamento
        </p>
      </div>

      <!-- Login card -->
      <Card class="public-layout__card">
        <div class="public-layout__card-body">
          <h1 class="public-layout__card-title">Entrar no Sistema</h1>

          <div class="public-layout__dev-notice" role="note" aria-label="Aviso de desenvolvimento">
            <span class="i-carbon-information-filled public-layout__dev-notice-icon" aria-hidden="true" />
            <p class="public-layout__dev-notice-text">
              Ambiente de desenvolvimento — autenticação mock habilitada.
            </p>
          </div>

          <Button
            variant="primary"
            size="lg"
            full-width
            icon-left="i-carbon-login"
            @click="handleLogin"
          >
            Entrar
          </Button>
        </div>
      </Card>
    </div>
  </div>
</template>

<style scoped>
.public-layout {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #0c141a;
  padding: 1.5rem;
}

.public-layout__container {
  width: 100%;
  max-width: 400px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2rem;
}

/* Branding */
.public-layout__brand {
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
}

.public-layout__brand-icon {
  font-size: 3rem;
  color: oklch(72% 0.18 220);
}

.public-layout__brand-text {
  font-size: 2rem;
  font-weight: 700;
  letter-spacing: 0.1em;
  color: oklch(95% 0.01 220);
}

.public-layout__brand-subtitle {
  font-size: 0.875rem;
  color: oklch(55% 0.04 220);
  margin: 0;
  text-align: center;
}

/* Card */
.public-layout__card {
  width: 100%;
}

.public-layout__card-body {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  padding: 0.25rem;
}

.public-layout__card-title {
  font-size: 1.25rem;
  font-weight: 600;
  color: var(--color-text-primary, #111827);
  margin: 0;
  text-align: center;
}

/* Dev notice */
.public-layout__dev-notice {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  background: var(--color-surface-elevated, #f3f4f6);
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 0.375rem;
  padding: 0.75rem;
}

.public-layout__dev-notice-icon {
  font-size: 1rem;
  color: var(--color-info, #3b82f6);
  flex-shrink: 0;
  margin-top: 0.125rem;
}

.public-layout__dev-notice-text {
  font-size: 0.8125rem;
  color: var(--color-text-secondary, #6b7280);
  margin: 0;
  line-height: 1.5;
}
</style>

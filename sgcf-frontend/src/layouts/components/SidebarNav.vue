<script setup lang="ts">
import { RouterLink } from 'vue-router'
import RoleGate from '@/shared/auth/RoleGate.vue'

interface NavItem {
  label: string
  to: string
}

interface NavSection {
  heading: string
  policy?: string
  items: NavItem[]
}

const sections: NavSection[] = [
  {
    heading: 'Painel',
    items: [
      { label: 'Dívida', to: '/painel' },
      { label: 'Garantias', to: '/painel/garantias' },
      { label: 'Vencimentos', to: '/painel/vencimentos' },
    ],
  },
  {
    heading: 'Painel',
    policy: 'Executivo',
    items: [{ label: 'KPIs Executivos', to: '/painel/kpis' }],
  },
  {
    heading: 'Painel',
    policy: 'Auditoria',
    items: [
      { label: 'Input EBITDA', to: '/painel/ebitda' },
      { label: 'Auditoria', to: '/auditoria' },
    ],
  },
  {
    heading: 'Contratos',
    items: [
      { label: 'Contratos', to: '/contratos' },
      { label: 'Bancos', to: '/bancos' },
      { label: 'Hedges', to: '/hedges' },
    ],
  },
  {
    heading: 'Simulador',
    policy: 'Executivo',
    items: [
      { label: 'Cenário Cambial', to: '/simulador/cenario-cambial' },
      { label: 'Antecipação de Portfólio', to: '/simulador/antecipacao-portfolio' },
    ],
  },
  {
    heading: 'Configuração',
    policy: 'Admin',
    items: [
      { label: 'Plano de Contas', to: '/plano-contas' },
      { label: 'Parâmetros de Cotação', to: '/parametros-cotacao' },
      { label: 'Feriados', to: '/feriados' },
    ],
  },
]

// Group sections by heading for rendering (adjacent same-heading sections collapse under one header)
interface RenderGroup {
  heading: string
  subsections: NavSection[]
}

function buildRenderGroups(input: NavSection[]): RenderGroup[] {
  const groups: RenderGroup[] = []
  for (const section of input) {
    const last = groups[groups.length - 1]
    if (last && last.heading === section.heading) {
      last.subsections.push(section)
    } else {
      groups.push({ heading: section.heading, subsections: [section] })
    }
  }
  return groups
}

const renderGroups = buildRenderGroups(sections)
</script>

<template>
  <nav class="sidebar-nav" aria-label="Navegação principal">
    <!-- Brand -->
    <div class="sidebar-brand" aria-label="SGCF">
      <span class="sidebar-brand__icon i-mdi-bank-outline" aria-hidden="true" />
      <span class="sidebar-brand__text">SGCF</span>
    </div>

    <!-- Navigation groups -->
    <div class="sidebar-menu">
      <template v-for="group in renderGroups" :key="group.heading">
        <div class="sidebar-section">
          <span class="sidebar-section__heading">{{ group.heading }}</span>

          <template v-for="subsection in group.subsections" :key="subsection.policy ?? '__open__'">
            <!-- Items that need a policy check -->
            <template v-if="subsection.policy">
              <RoleGate :policy="subsection.policy">
                <RouterLink
                  v-for="item in subsection.items"
                  :key="item.to"
                  :to="item.to"
                  class="sidebar-nav-item"
                  active-class="sidebar-nav-item--active"
                  exact-active-class="sidebar-nav-item--exact-active"
                >
                  {{ item.label }}
                </RouterLink>
              </RoleGate>
            </template>

            <!-- Unrestricted items -->
            <template v-else>
              <RouterLink
                v-for="item in subsection.items"
                :key="item.to"
                :to="item.to"
                class="sidebar-nav-item"
                active-class="sidebar-nav-item--active"
                exact-active-class="sidebar-nav-item--exact-active"
              >
                {{ item.label }}
              </RouterLink>
            </template>
          </template>
        </div>
      </template>
    </div>
  </nav>
</template>

<style scoped>
.sidebar-nav {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow-y: auto;
  padding: 0;
}

/* Brand */
.sidebar-brand {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  padding: 1.25rem 1.25rem 1rem;
  border-bottom: 1px solid oklch(30% 0.04 220 / 0.4);
  flex-shrink: 0;
}

.sidebar-brand__icon {
  font-size: 1.5rem;
  color: oklch(72% 0.18 220);
}

.sidebar-brand__text {
  font-size: 1.25rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: oklch(95% 0.01 220);
}

/* Menu */
.sidebar-menu {
  flex: 1;
  padding: 1rem 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

/* Section */
.sidebar-section {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  padding: 0 0.75rem;
  margin-bottom: 0.5rem;
}

.sidebar-section__heading {
  font-size: 0.6875rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: oklch(55% 0.04 220);
  padding: 0.5rem 0.5rem 0.25rem;
}

/* Nav items */
.sidebar-nav-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  border-radius: 0.375rem;
  font-size: 0.875rem;
  font-weight: 400;
  color: oklch(70% 0.04 220);
  text-decoration: none;
  transition: background 0.15s ease, color 0.15s ease;
  cursor: pointer;
}

.sidebar-nav-item:hover {
  background: oklch(22% 0.04 220 / 0.6);
  color: oklch(90% 0.04 220);
}

.sidebar-nav-item--active,
.sidebar-nav-item--exact-active {
  background: oklch(20% 0.08 220);
  color: oklch(75% 0.18 220);
  font-weight: 500;
}

.sidebar-nav-item--active:hover,
.sidebar-nav-item--exact-active:hover {
  background: oklch(22% 0.09 220);
}
</style>

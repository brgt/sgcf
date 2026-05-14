import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../shared/auth/useAuth'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/painel',
  },
  {
    path: '/login',
    component: () => import('../layouts/PublicLayout.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    component: () => import('../layouts/AppShell.vue'),
    children: [
      { path: 'painel', component: () => import('../features/painel/pages/PainelDividaPage.vue'), meta: { title: 'Painel de Dívida' } },
      { path: 'painel/garantias', component: () => import('../features/painel/pages/PainelGarantiasPage.vue'), meta: { title: 'Painel de Garantias' } },
      { path: 'painel/vencimentos', component: () => import('../features/painel/pages/CalendarioVencimentosPage.vue'), meta: { title: 'Calendário de Vencimentos' } },
      { path: 'painel/kpis', component: () => import('../features/painel/pages/KpisExecutivoPage.vue'), meta: { policy: 'Executivo', title: 'KPIs Executivos' } },
      { path: 'painel/ebitda', component: () => import('../features/painel/pages/EbitdaInputPage.vue'), meta: { policy: 'Auditoria', title: 'Input EBITDA' } },
      { path: 'contratos', component: () => import('../features/contratos/pages/ContratosListPage.vue'), meta: { title: 'Contratos' } },
      { path: 'contratos/novo', component: () => import('../features/contratos/pages/ContratoCreatePage.vue'), meta: { policy: 'Escrita', title: 'Novo Contrato' } },
      { path: 'contratos/:id', component: () => import('../features/contratos/pages/ContratoDetailPage.vue'), meta: { title: 'Detalhe do Contrato' } },
      { path: 'bancos', component: () => import('../features/bancos/pages/BancosListPage.vue'), meta: { title: 'Bancos' } },
      { path: 'bancos/novo', component: () => import('../features/bancos/pages/BancoFormPage.vue'), meta: { policy: 'Admin', title: 'Novo Banco' } },
      { path: 'bancos/:id', component: () => import('../features/bancos/pages/BancoDetailPage.vue'), meta: { title: 'Detalhe do Banco' } },
      { path: 'bancos/:id/editar-config', component: () => import('../features/bancos/pages/BancoFormPage.vue'), meta: { policy: 'Admin', title: 'Editar Config. Antecipação' } },
      { path: 'hedges', component: () => import('../features/hedges/pages/HedgesListPage.vue'), meta: { title: 'Hedges' } },
      { path: 'simulador/cenario-cambial', component: () => import('../features/simulador/pages/CenarioCambialPage.vue'), meta: { policy: 'Executivo', title: 'Cenário Cambial' } },
      { path: 'simulador/antecipacao-portfolio', component: () => import('../features/simulador/pages/AntecipacaoPortfolioPage.vue'), meta: { policy: 'Executivo', title: 'Antecipação de Portfólio' } },
      { path: 'plano-contas', component: () => import('../features/plano-contas/pages/PlanoContasListPage.vue'), meta: { title: 'Plano de Contas' } },
      { path: 'parametros-cotacao', component: () => import('../features/parametros-cotacao/pages/ParametrosListPage.vue'), meta: { title: 'Parâmetros de Cotação' } },
      { path: 'feriados', component: () => import('../features/feriados/pages/FeriadosListPage.vue'), meta: { title: 'Feriados' } },
      { path: 'auditoria', component: () => import('../features/auditoria/pages/AuditoriaPage.vue'), meta: { title: 'Auditoria' } },
    ],
  },
  { path: '/403', component: () => import('../layouts/ForbiddenPage.vue') },
  { path: '/:pathMatch(.*)*', component: () => import('../layouts/NotFoundPage.vue') },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  const auth = useAuthStore()

  // Allow public routes (login, etc.)
  if (to.meta['public']) return true

  // Require authentication
  if (!auth.isAuthenticated) {
    return { path: '/login' }
  }

  // Check policy requirement
  const policy = to.meta['policy'] as string | undefined
  if (policy && !auth.hasPolicy(policy)) {
    return { path: '/403' }
  }

  return true
})

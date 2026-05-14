// URL constants — all API paths relative to /api prefix (Vite proxies to backend)
export const API = {
  bancos: {
    list:         '/api/v1/bancos',
    get:          (id: string) => `/api/v1/bancos/${id}`,
    create:       '/api/v1/bancos',
    updateConfig: (id: string) => `/api/v1/bancos/${id}/config-antecipacao`,
  },
  contratos: {
    list:               '/api/v1/contratos',
    get:                (id: string) => `/api/v1/contratos/${id}`,
    create:             '/api/v1/contratos',
    delete:             (id: string) => `/api/v1/contratos/${id}`,
    tabelaCompleta:     (id: string) => `/api/v1/contratos/${id}/tabela-completa`,
    gerarCronograma:    (id: string) => `/api/v1/contratos/${id}/gerar-cronograma`,
    simularAntecipacao: (id: string) => `/api/v1/contratos/${id}/simular-antecipacao`,
    importarCronograma: (id: string) => `/api/v1/contratos/${id}/importar-cronograma`,
    garantias:          (id: string) => `/api/v1/contratos/${id}/garantias`,
    garantia:           (id: string, garantiaId: string) => `/api/v1/contratos/${id}/garantias/${garantiaId}`,
    garantiaIndicadores: (id: string) => `/api/v1/contratos/${id}/garantias/indicadores`,
    hedges:             (id: string) => `/api/v1/contratos/${id}/hedges`,
  },
  hedges: {
    get:    (id: string) => `/api/v1/hedges/${id}`,
    mtm:    (id: string) => `/api/v1/hedges/${id}/mtm`,
    delete: (id: string) => `/api/v1/hedges/${id}`,
  },
  painel: {
    divida:      '/api/v1/painel/divida',
    garantias:   '/api/v1/painel/garantias',
    vencimentos: '/api/v1/painel/vencimentos',
    kpis:        '/api/v1/painel/kpis',
    ebitda:      '/api/v1/painel/ebitda',
  },
  parametrosCotacao: {
    list:    '/api/v1/parametros-cotacao',
    get:     (id: string) => `/api/v1/parametros-cotacao/${id}`,
    create:  '/api/v1/parametros-cotacao',
    update:  (id: string) => `/api/v1/parametros-cotacao/${id}`,
    resolve: '/api/v1/parametros-cotacao/resolve',
  },
  planoContas: {
    list:   '/api/v1/plano-contas',
    get:    (id: string) => `/api/v1/plano-contas/${id}`,
    create: '/api/v1/plano-contas',
    update: (id: string) => `/api/v1/plano-contas/${id}`,
  },
  simulador: {
    cenarioCambial:       '/api/v1/simulador/cenario-cambial',
    antecipacaoPortfolio: '/api/v1/simulador/antecipacao-portfolio',
  },
  health: '/health',
} as const

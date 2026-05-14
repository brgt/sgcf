// Mirror of Sgcf.Domain.Common.Moeda
export const Moeda = {
  Brl: 'Brl',
  Usd: 'Usd',
  Eur: 'Eur',
  Jpy: 'Jpy',
  Cny: 'Cny',
} as const
export type Moeda = (typeof Moeda)[keyof typeof Moeda]

// Mirror of Sgcf.Domain.Contratos.ModalidadeContrato
export const ModalidadeContrato = {
  Finimp:      'Finimp',
  Refinimp:    'Refinimp',
  Lei4131:     'Lei4131',
  Nce:         'Nce',
  BalcaoCaixa: 'BalcaoCaixa',
  Fgi:         'Fgi',
} as const
export type ModalidadeContrato = (typeof ModalidadeContrato)[keyof typeof ModalidadeContrato]

// Mirror of Sgcf.Domain.Contratos.StatusContrato
export const StatusContrato = {
  Ativo:               'Ativo',
  Liquidado:           'Liquidado',
  Vencido:             'Vencido',
  Inadimplente:        'Inadimplente',
  Cancelado:           'Cancelado',
  RefinanciadoParcial: 'RefinanciadoParcial',
  RefinanciadoTotal:   'RefinanciadoTotal',
} as const
export type StatusContrato = (typeof StatusContrato)[keyof typeof StatusContrato]

// Mirror of Sgcf.Domain.Hedge.TipoHedge
export const TipoHedge = {
  Ndf:    'Ndf',
  Collar: 'Collar',
  Swap:   'Swap',
  Opcao:  'Opcao',
} as const
export type TipoHedge = (typeof TipoHedge)[keyof typeof TipoHedge]

// Mirror of Sgcf.Domain.Cotacoes.TipoCotacao
export const TipoCotacao = {
  PtaxD1:       'PtaxD1',
  SpotIntraday: 'SpotIntraday',
  Manual:       'Manual',
} as const
export type TipoCotacao = (typeof TipoCotacao)[keyof typeof TipoCotacao]

// Mirror of Sgcf.Domain.Contratos.TipoGarantia
export const TipoGarantia = {
  Cdb:                'Cdb',
  Sblc:               'Sblc',
  Aval:               'Aval',
  AlienacaoFiduciaria: 'AlienacaoFiduciaria',
  Duplicatas:         'Duplicatas',
  RecebiveisCartao:   'RecebiveisCartao',
  BoletoBancario:     'BoletoBancario',
  Fgi:                'Fgi',
} as const
export type TipoGarantia = (typeof TipoGarantia)[keyof typeof TipoGarantia]

// Mirror of Sgcf.Domain.Contratos.StatusGarantia
export const StatusGarantia = {
  Ativa:    'Ativa',
  Liberada: 'Liberada',
  Expirada: 'Expirada',
} as const
export type StatusGarantia = (typeof StatusGarantia)[keyof typeof StatusGarantia]

// Mirror of Sgcf.Domain.Contratos.BaseCalculo
export const BaseCalculo = {
  Du252: 'Du252',
  Dc360: 'Dc360',
  Dc365: 'Dc365',
} as const
export type BaseCalculo = (typeof BaseCalculo)[keyof typeof BaseCalculo]

// Mirror of Sgcf.Domain.Contratos.PadraoAntecipacao
export const PadraoAntecipacao = {
  LiquidacaoTotal:   'LiquidacaoTotal',
  LiquidacaoParcial: 'LiquidacaoParcial',
  AmbosOsModelos:    'AmbosOsModelos',
} as const
export type PadraoAntecipacao = (typeof PadraoAntecipacao)[keyof typeof PadraoAntecipacao]

// Mirror of Sgcf.Domain.Contratos.Periodicidade
export const Periodicidade = {
  Bullet:     'Bullet',
  Mensal:     'Mensal',
  Bimestral:  'Bimestral',
  Trimestral: 'Trimestral',
  Semestral:  'Semestral',
  Anual:      'Anual',
} as const
export type Periodicidade = (typeof Periodicidade)[keyof typeof Periodicidade]

// Mirror of Sgcf.Domain.Contratos.EstruturaAmortizacao
export const EstruturaAmortizacao = {
  Bullet:     'Bullet',
  Price:      'Price',
  Sac:        'Sac',
  Customizada:'Customizada',
} as const
export type EstruturaAmortizacao = (typeof EstruturaAmortizacao)[keyof typeof EstruturaAmortizacao]

// Mirror of Sgcf.Domain.Contratos.AnchorDiaMes
export const AnchorDiaMes = {
  DiaContratacao: 'DiaContratacao',
  DiaFixo:        'DiaFixo',
  UltimoDiaMes:   'UltimoDiaMes',
} as const
export type AnchorDiaMes = (typeof AnchorDiaMes)[keyof typeof AnchorDiaMes]

// Mirror of Sgcf.Domain.Contratos.ConvencaoDataNaoUtil
export const ConvencaoDataNaoUtil = {
  Following:         'Following',
  ModifiedFollowing: 'ModifiedFollowing',
  Preceding:         'Preceding',
  NoAdjustment:      'NoAdjustment',
} as const
export type ConvencaoDataNaoUtil = (typeof ConvencaoDataNaoUtil)[keyof typeof ConvencaoDataNaoUtil]

// Mirror of Sgcf.Domain.Calendario.EscopoFeriado
export const EscopoFeriado = {
  Nacional:  'Nacional',
  Estadual:  'Estadual',
  Municipal: 'Municipal',
} as const
export type EscopoFeriado = (typeof EscopoFeriado)[keyof typeof EscopoFeriado]

// Mirror of Sgcf.Domain.Calendario.TipoFeriado
export const TipoFeriado = {
  FixoCalendario:  'FixoCalendario',
  MovelCalendario: 'MovelCalendario',
  Pontual:         'Pontual',
} as const
export type TipoFeriado = (typeof TipoFeriado)[keyof typeof TipoFeriado]

// Mirror of Sgcf.Domain.Calendario.FonteFeriado
export const FonteFeriado = {
  Manual: 'Manual',
  Anbima: 'Anbima',
} as const
export type FonteFeriado = (typeof FonteFeriado)[keyof typeof FonteFeriado]

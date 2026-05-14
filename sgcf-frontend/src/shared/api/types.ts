import type {
  Moeda,
  ModalidadeContrato,
  StatusContrato,
  TipoHedge,
  TipoCotacao,
  TipoGarantia,
  StatusGarantia,
  BaseCalculo,
  PadraoAntecipacao,
} from './enums'

// ============================================================================
// COMMON
// ============================================================================

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

// ============================================================================
// BANCOS
// ============================================================================

export interface BancoDto {
  id: string
  codigoCompe: string
  razaoSocial: string
  apelido: string
  aceitaLiquidacaoTotal: boolean
  aceitaLiquidacaoParcial: boolean
  exigeAnuenciaExpressa: boolean
  exigeParcelaInteira: boolean
  avisoPrevioMinDiasUteis: number
  padraoAntecipacao: PadraoAntecipacao
  valorMinimoParcialPct: number | null
  breakFundingFeePct: number | null
  tlaPctSobreSaldo: number | null
  tlaPctPorMesRemanescente: number | null
  observacoesAntecipacao: string | null
  limiteCreditoBrl: number | null
  /** ISO 8601 instant */
  createdAt: string
  updatedAt: string
}

export interface CreateBancoCommand {
  codigoCompe: string
  razaoSocial: string
  apelido: string
  padraoAntecipacao: PadraoAntecipacao
}

export interface UpdateBancoConfigRequest {
  aceitaLiquidacaoTotal: boolean
  aceitaLiquidacaoParcial: boolean
  exigeAnuenciaExpressa: boolean
  exigeParcelaInteira: boolean
  avisoPrevioMinDiasUteis: number
  padraoAntecipacao: PadraoAntecipacao
  valorMinimoParcialPct: number | null
  breakFundingFeePct: number | null
  tlaPctSobreSaldo: number | null
  tlaPctPorMesRemanescente: number | null
  observacoesAntecipacao: string | null
}

// ============================================================================
// CONTRATOS
// ============================================================================

export interface ParcelaDto {
  id: string
  numero: number
  /** yyyy-MM-dd */
  dataVencimento: string
  valorPrincipal: number
  valorJuros: number
  valorPago: number | null
  moeda: Moeda
  status: string
  dataPagamento: string | null
}

export interface GarantiaCdbDto {
  bancoCustodia: string
  numeroCdb: string
  dataEmissaoCdb: string
  dataVencimentoCdb: string
  rendimentoAaPct: number | null
  percentualCdiPct: number | null
  taxaIrrfAplicacaoPct: number | null
}

export interface GarantiaSblcDto {
  bancoEmissor: string
  paisEmissor: string
  swiftCode: string | null
  validadeDias: number
  comissaoAaPct: number | null
  numeroSblc: string | null
}

export interface GarantiaAvalDto {
  avalistaTipo: string
  avalistaNome: string
  avialistaDocumento: string
  valorAvalBrl: number
  vigenciaAte: string | null
}

export interface GarantiaAlienacaoDto {
  tipoBem: string
  descricaoBem: string
  valorAvaliadoBrl: number
  matriculaOuChassi: string | null
  cartorioRegistro: string | null
}

export interface GarantiaDuplicatasDto {
  percentualDescontoPct: number
  vencimentoEscalonadoInicio: string
  vencimentoEscalonadoFim: string
  qtdDuplicatasCedidas: number
  valorTotalDuplicatasBrl: number
  instrumentoCessaoData: string | null
}

export interface GarantiaRecebiveisDto {
  operadoraCartao: string
  tipoRecebivel: string
  percentualFaturamentoPct: number
  valorMedioMensalBrl: number | null
  prazoRecebimentoDias: number
  termoCessaoUrl: string | null
}

export interface GarantiaBoletoBancarioDto {
  bancoEmissor: string
  quantidadeBoletos: number
  valorUnitarioBrl: number
  dataEmissaoInicial: string
  dataVencimentoInicial: string
  dataVencimentoFinal: string
  periodicidade: string
}

export interface GarantiaFgiDto {
  tipoFgi: string
  percentualCoberturaPct: number
  taxaFgiAaPct: number | null
  bancoIntermediario: string | null
  codigoOperacaoBndes: string | null
}

export interface GarantiaDto {
  id: string
  contratoId: string
  tipo: TipoGarantia
  valorBrl: number
  percentualPrincipalPct: number | null
  dataConstituicao: string
  dataLiberacaoPrevista: string | null
  dataLiberacaoEfetiva: string | null
  status: StatusGarantia
  observacoes: string | null
  cdb: GarantiaCdbDto | null
  sblc: GarantiaSblcDto | null
  aval: GarantiaAvalDto | null
  alienacao: GarantiaAlienacaoDto | null
  duplicatas: GarantiaDuplicatasDto | null
  recebiveis: GarantiaRecebiveisDto | null
  boleto: GarantiaBoletoBancarioDto | null
  fgi: GarantiaFgiDto | null
}

export interface HedgeDto {
  id: string
  contratoId: string
  tipo: TipoHedge
  contraparteId: string
  notionalMoedaOriginal: number
  moedaBase: Moeda
  dataContratacao: string
  dataVencimento: string
  strikeForward: number | null
  strikePut: number | null
  strikeCall: number | null
  status: string
}

export interface MtmResultadoDto {
  mtmAReceberBrl: number
  mtmAPagarBrl: number
  mtmLiquidoBrl: number
  dataCalculo: string
  tipoCotacao: TipoCotacao
}

// Modality detail DTOs

export interface FinimpDetailDto {
  rofNumero: string | null
  rofDataEmissao: string | null
  exportadorNome: string | null
  exportadorPais: string | null
  produtoImportado: string | null
  faturaReferencia: string | null
  incoterm: string | null
  breakFundingFeePercentual: number | null
  temMarketFlex: boolean
}

export interface Lei4131DetailDto {
  sblcNumero: string | null
  sblcBancoEmissor: string | null
  sblcValorUsd: number | null
  temMarketFlex: boolean
  breakFundingFeePercentual: number | null
}

export interface RefinimpDetailDto {
  contratoMaeId: string
  percentualRefinanciado: number
}

export interface NceDetailDto {
  nceNumero: string | null
  dataEmissao: string | null
  bancoMandatario: string | null
}

export interface BalcaoCaixaDetailDto {
  numeroOperacao: string | null
  tipoProduto: string | null
  temFgi: boolean
}

export interface FgiDetailDto {
  numeroOperacaoFgi: string | null
  taxaFgiAaPct: number | null
  percentualCobertoPct: number | null
}

export interface ContratoDto {
  id: string
  numeroExterno: string
  codigoInterno: string | null
  bancoId: string
  modalidade: ModalidadeContrato
  moeda: Moeda
  valorPrincipal: number
  dataContratacao: string
  dataVencimento: string
  taxaAa: number
  baseCalculo: BaseCalculo
  status: StatusContrato
  contratoPaiId: string | null
  observacoes: string | null
  createdAt: string
  updatedAt: string
  parcelas: ParcelaDto[]
  garantias: GarantiaDto[]
  hedges: HedgeDto[]
  finimpDetail: FinimpDetailDto | null
  lei4131Detail: Lei4131DetailDto | null
  refinimpDetail: RefinimpDetailDto | null
  nceDetail: NceDetailDto | null
  balcaoCaixaDetail: BalcaoCaixaDetailDto | null
  fgiDetail: FgiDetailDto | null
}

// ============================================================================
// PAINEL
// ============================================================================

export interface LinhaBreakdownMoedaDto {
  moeda: Moeda
  saldoMoedaOriginal: number
  cotacaoAplicada: number
  saldoBrl: number
  quantidadeContratos: number
}

export interface AjusteMtmDto {
  mtmAReceberBrl: number
  mtmAPagarBrl: number
  mtmLiquidoBrl: number
}

export interface PainelDividaDto {
  dataHoraCalculo: string
  tipoCotacao: TipoCotacao
  breakdownPorMoeda: LinhaBreakdownMoedaDto[]
  dividaBrutaBrl: number
  ajusteMtm: AjusteMtmDto
  dividaLiquidaPosHedgeBrl: number
  alertas: string[]
}

export interface LinhaDistribuicaoTipoDto {
  tipo: string
  valorBrl: number
  percentualDoTotal: number
}

export interface LinhaDistribuicaoBancoDto {
  bancoId: string
  valorBrl: number
  percentualDoTotal: number
}

export interface PainelGarantiasDto {
  dataCalculo: string
  totalGarantiasAtivasBrl: number
  distribuicaoPorTipo: LinhaDistribuicaoTipoDto[]
  distribuicaoPorBanco: LinhaDistribuicaoBancoDto[]
  alertas: string[]
}

export interface IndicadoresGarantiaDto {
  coberturaTotalBrl: number
  percentualCoberturaTotalPct: number
  coberturaLiquidaSemCdbBrl: number
  percentualCoberturaLiquidaPct: number
  percentualFaturamentoCartaoComprometidoPct: number
  alertas: string[]
}

export interface CalendarioMesDto {
  mes: number
  totalBrl: number
  quantidadeParcelas: number
}

export interface CalendarioVencimentosDto {
  ano: number
  meses: CalendarioMesDto[]
  totalAnoBrl: number
}

export interface ConcentracaoBancoDto {
  bancoId: string
  apelido: string
  percentual: number
}

export interface KpiDto {
  dividaTotalBrl: number
  custoMedioPonderadoAa: number
  prazoMedioMeses: number
  concentracaoPorBanco: ConcentracaoBancoDto[]
}

// ============================================================================
// PLANO DE CONTAS
// ============================================================================

export interface PlanoContasDto {
  id: string
  codigoGerencial: string
  nome: string
  natureza: string
  codigoSapB1: string | null
  ativo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreatePlanoContasCommand {
  codigoGerencial: string
  nome: string
  natureza: string
  codigoSapB1: string | null
}

export interface AtualizarContaRequest {
  nome: string
  natureza: string
  codigoSapB1: string | null
}

// ============================================================================
// PARÂMETROS DE COTAÇÃO
// ============================================================================

export interface ParametroCotacaoDto {
  id: string
  bancoId: string | null
  modalidade: ModalidadeContrato | null
  tipoCotacao: TipoCotacao
  ativo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateParametroCommand {
  tipoCotacao: TipoCotacao
  ativo: boolean
  bancoId: string | null
  modalidade: ModalidadeContrato | null
}

export interface ResolveTipoCotacaoResponse {
  tipoCotacao: TipoCotacao
}

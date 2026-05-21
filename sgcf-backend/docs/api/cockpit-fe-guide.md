# Cockpit FE Guide — Não-Gaps (o FE resolve sem novo endpoint de backend)

**Versão:** 1.0  
**Data:** 2026-05-21  
**Audiência:** Time de frontend do cockpit multi-persona  
**Referência do plano:** `tasks/plan_cockpit_backend_gaps.md` §1.1 e Task 2.2  

---

## Visão geral

Cinco dos vinte e seis gaps identificados na análise do cockpit multi-persona (2026-05-20) foram classificados como **Não-Gaps**: o backend já expõe todos os dados necessários; a agregação, o agrupamento e os cálculos derivados podem ser feitos inteiramente no cliente.

Este guia fornece, para cada não-gap, a URL exata do endpoint, os campos do DTO que devem ser consumidos e um snippet TypeScript mostrando a transformação client-side.

| Gap | Indicador no cockpit | Persona | Estratégia FE |
|-----|----------------------|---------|---------------|
| GAP-CKP-05 | Pipeline de cotações por estágio | Gerente Financeiro | Agregar `CotacaoDto.Status` via `GET /cotacoes?status=` |
| GAP-CKP-06 | Contratos a vencer por janela | Gerente Financeiro | 4 chamadas paralelas a `GET /contratos` com `vencDe`/`vencAte` |
| GAP-CKP-14 (CDI) | Economia CDI negociada | CFO | Campo `TotalEconomiaAjustadaCdiBrl` de `GET /cotacoes/economia` |
| GAP-CKP-19 (parcial) | Spread proposta × contrato final | Gerente Financeiro | Campos `SpreadAaPercentual` e `TaxaAaPercentual` de `PropostaDto` |
| GAP-CKP-20 | Headroom de crédito por banco | Gerente Financeiro | Campos `ValorLimiteBrl`, `ValorUtilizadoBrl`, `ValorDisponivelBrl` de `LimiteBancoDto` |

> **Gaps que ainda exigem novo backend:** GAP-14 para benchmarks SOFR/Selic (Task 4.4 futura) e GAP-19 para `TaxaIndicativaAa` (Task 4.10 futura). As seções abaixo indicam esses limites explicitamente.

---

## GAP-CKP-05 — Pipeline de Cotações por Estágio

**Classificação:** Não-Gap (agregação client-side)

### Endpoint disponível

```
GET /api/v1/cotacoes
Autorização: Leitura
```

**Query Parameters relevantes:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `status` | string | Filtra por `StatusCotacao`. Omitir retorna todos os estágios. |
| `modalidade` | string | Opcional. Filtra por modalidade de captação. |
| `desde` | date (`YYYY-MM-DD`) | Data mínima de abertura |
| `ate` | date (`YYYY-MM-DD`) | Data máxima de abertura |
| `page` | int | Padrão `1` |
| `pageSize` | int | Padrão `20`, máx `100` |

**Campos de `CotacaoDto` usados:**

| Campo | Tipo | Uso |
|-------|------|-----|
| `status` | string | Valor do enum `StatusCotacao` serializado como string |
| `modalidade` | string | Agrupamento opcional por modalidade |
| `valorAlvoBrl` | decimal | Volume em BRL de cada cotação |

**Resposta:** `PagedResult<CotacaoDto>`

```json
{
  "items": [
    {
      "id": "019e21cc-...",
      "status": "EmCaptacao",
      "modalidade": "Finimp",
      "valorAlvoBrl": 5000000.00,
      ...
    }
  ],
  "total": 47,
  "page": 1,
  "pageSize": 100
}
```

### Como implementar no FE

O funil do cockpit exige contar cotações (e opcionalmente somar volume) por estágio. A estratégia correta é buscar sem filtro de `status` com `pageSize=100` (ou paginar até esgotar) e agrupar client-side.

```typescript
// features/cockpit/composables/useFunilCotacoes.ts

import { useQuery } from '@tanstack/vue-query'
import { cotacoesApi } from '@/features/cotacoes/api/cotacoesApi'

// Todos os estágios visíveis no funil do Gerente Financeiro
const ESTAGIOS_FUNIL = [
  'Rascunho',
  'EmCaptacao',
  'EmAnaliseBanco',
  'PropostaRecebida',
  'Comparada',
  'Aceita',
  'Convertida',
] as const

type EstagioFunil = typeof ESTAGIOS_FUNIL[number]

interface ItemFunil {
  estagio: EstagioFunil
  quantidade: number
  volumeBrl: number
}

async function fetchTodasCotacoesAtivas(): Promise<CotacaoDto[]> {
  // Busca sem filtro de status; pageSize máx = 100
  // Para carteiras grandes (>100 cotações abertas), paginar até `total` esgotar
  const primeira = await cotacoesApi.list({ pageSize: 100, page: 1 })
  const todas: CotacaoDto[] = [...primeira.items]

  const totalPaginas = Math.ceil(primeira.total / 100)
  const requisicoesPendentes = Array.from({ length: totalPaginas - 1 }, (_, i) =>
    cotacoesApi.list({ pageSize: 100, page: i + 2 }),
  )
  const demaisPaginas = await Promise.all(requisicoesPendentes)
  demaisPaginas.forEach((p) => todas.push(...p.items))

  return todas
}

export function useFunilCotacoes() {
  return useQuery({
    queryKey: ['cockpit', 'funil-cotacoes'],
    queryFn: async (): Promise<ItemFunil[]> => {
      const cotacoes = await fetchTodasCotacoesAtivas()

      // Agrupar por estágio
      const mapa = new Map<EstagioFunil, { quantidade: number; volumeBrl: number }>()
      for (const estagio of ESTAGIOS_FUNIL) {
        mapa.set(estagio, { quantidade: 0, volumeBrl: 0 })
      }

      for (const c of cotacoes) {
        const estagio = c.status as EstagioFunil
        if (mapa.has(estagio)) {
          const atual = mapa.get(estagio)!
          mapa.set(estagio, {
            quantidade: atual.quantidade + 1,
            volumeBrl: atual.volumeBrl + c.valorAlvoBrl,
          })
        }
      }

      return ESTAGIOS_FUNIL.map((estagio) => ({
        estagio,
        ...mapa.get(estagio)!,
      }))
    },
    staleTime: 60_000, // funil muda raramente; 1 min de cache é aceitável
  })
}
```

### Nota pós-Task 0.5 — StatusCotacao expandido

A Task 0.5 adicionou dois estágios intermediários ao enum `StatusCotacao`:

| Valor | Byte | Descrição |
|-------|------|-----------|
| `EmAnaliseBanco` | `7` | Banco confirmou recebimento e está analisando |
| `PropostaRecebida` | `8` | Ao menos uma proposta foi registrada na cotação |

O ciclo completo após a Task 0.5 é:

```
Rascunho → EmCaptacao → EmAnaliseBanco → PropostaRecebida → Comparada → Aceita → Convertida
                    ↘ Recusada (qualquer estágio anterior a Convertida)
```

O snippet acima já inclui `EmAnaliseBanco` e `PropostaRecebida` na constante `ESTAGIOS_FUNIL`. Antes da Task 0.5 esses valores não existem no backend — o FE deve filtrar os estágios conhecidos dinamicamente se precisar suportar ambas as versões do backend.

**Limitação:** `pageSize` máximo é `100`. Carteiras com mais de 100 cotações abertas exigem paginação sequencial conforme o snippet acima. Para carteiras muito grandes (>500 cotações), considerar janela de datas (`desde`/`ate`) para limitar o conjunto.

---

## GAP-CKP-06 — Contratos por Janela de Vencimento

**Classificação:** Não-Gap (4 chamadas paralelas)

### Endpoint disponível

```
GET /api/v1/contratos
Autorização: Leitura
```

**Query Parameters relevantes:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `status` | string | Use `Ativo` para excluir encerrados |
| `vencDe` | string (`YYYY-MM-DD`) | Data mínima de vencimento |
| `vencAte` | string (`YYYY-MM-DD`) | Data máxima de vencimento |
| `bancoId` | guid | Opcional — filtra por banco |
| `modalidade` | string | Opcional — filtra por modalidade |
| `moeda` | string | Opcional — filtra por moeda |
| `page` | int | Padrão `1` |
| `pageSize` | int | Padrão `25`, máx `100` |

**Campos de `ContratoDto` usados para o card de vencimentos:**

| Campo | Tipo | Uso |
|-------|------|-----|
| `total` | int | Contagem total de contratos na janela |
| `items[].valorPrincipalBrl` | decimal | Soma do saldo em BRL |
| `items[].dataVencimento` | date | Data de vencimento |

**Resposta:** `PagedResult<ContratoDto>`

### Como implementar no FE (4 chamadas paralelas)

O cockpit exibe quatro cards: "Vence em 30 dias", "31–60 dias", "61–90 dias" e "91–180 dias". Cada card é resolvido com uma chamada independente, todas disparadas em paralelo.

```typescript
// features/cockpit/composables/useContratosAVencer.ts

import { useQuery } from '@tanstack/vue-query'
import { contratosApi } from '@/features/contratos/api/contratosApi'
import { LocalDate, DateTimeFormatter } from '@js-joda/core'

const FMT = DateTimeFormatter.ofPattern('yyyy-MM-dd')

interface JanelaVencimento {
  rotulo: '30d' | '31-60d' | '61-90d' | '91-180d'
  diasDe: number
  diasAte: number
}

const JANELAS: JanelaVencimento[] = [
  { rotulo: '30d', diasDe: 1, diasAte: 30 },
  { rotulo: '31-60d', diasDe: 31, diasAte: 60 },
  { rotulo: '61-90d', diasDe: 61, diasAte: 90 },
  { rotulo: '91-180d', diasDe: 91, diasAte: 180 },
]

interface CardVencimento {
  rotulo: string
  quantidade: number
  totalBrl: number
}

export function useContratosAVencer(hoje: LocalDate) {
  return useQuery({
    queryKey: ['cockpit', 'contratos-a-vencer', hoje.toString()],
    queryFn: async (): Promise<CardVencimento[]> => {
      // Disparar as 4 chamadas em paralelo
      const resultados = await Promise.all(
        JANELAS.map(({ diasDe, diasAte }) => {
          const vencDe = hoje.plusDays(diasDe).format(FMT)
          const vencAte = hoje.plusDays(diasAte).format(FMT)
          return contratosApi.list({
            status: 'Ativo',
            vencDe,
            vencAte,
            pageSize: 100, // para calcular o total correto — ver nota abaixo
          })
        }),
      )

      return JANELAS.map(({ rotulo }, idx) => {
        const pagina = resultados[idx]!
        // `total` da PagedResult dá a contagem real mesmo quando há mais de 100 registros
        const totalBrl = pagina.items.reduce((acc, c) => acc + c.valorPrincipalBrl, 0)
        return { rotulo, quantidade: pagina.total, totalBrl }
      })
    },
    staleTime: 5 * 60_000, // 5 minutos
  })
}
```

### Exemplo de chamada para a janela de 30 dias

```
GET /api/v1/contratos?status=Ativo&vencDe=2026-05-22&vencAte=2026-06-20&pageSize=100
Authorization: Bearer <token>
```

Resposta relevante:

```json
{
  "items": [ ... ],
  "total": 12,
  "page": 1,
  "pageSize": 100
}
```

O campo `total` da `PagedResult` retorna a contagem real de contratos que satisfazem o filtro, independentemente do `pageSize`. Portanto, `total` pode ser usado diretamente para o card — não é necessário somar `items.length`.

Para obter o `totalBrl` corretamente quando `total > pageSize`, paginar as páginas restantes e somar `valorPrincipalBrl` de todos os itens. Para carteiras muito grandes, considerar exibir apenas a contagem (`total`) no card sem o valor monetário agregado, evitando múltiplas chamadas por janela.

---

## GAP-CKP-14 — Economia CDI (parcial)

**Classificação:** Não-Gap para CDI; Real Gap para SOFR/Selic (Task 4.4 futura)

### Endpoint disponível

```
GET /api/v1/cotacoes/economia
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `de` | string (`YYYY-MM`) | Sim | Início do período |
| `ate` | string (`YYYY-MM`) | Sim | Fim do período |
| `bancoId` | guid | Não | Filtra por banco |

**Campos de `EconomiaPeriodoDto` usados:**

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `totalEconomiaBrutaBrl` | decimal | Economia bruta total no período em BRL |
| `totalEconomiaAjustadaCdiBrl` | decimal | Economia equalizada pelo CDI — métrica principal do cockpit |
| `totalOperacoes` | int | Quantidade de cotações convertidas no período |
| `porMes[].economiaBrutaBrl` | decimal | Economia bruta por mês |
| `porMes[].economiaAjustadaCdiBrl` | decimal | Economia ajustada CDI por mês |
| `porBanco[].economiaBrutaBrl` | decimal | Economia bruta por banco |
| `porBanco[].economiaAjustadaCdiBrl` | decimal | Economia ajustada CDI por banco |

### Como implementar no FE

```typescript
// features/cockpit/composables/useEconomiaCdi.ts

import { useQuery } from '@tanstack/vue-query'
import { cotacoesApi } from '@/features/cotacoes/api/cotacoesApi'

interface EconomiaCockpit {
  totalBrutaBrl: number
  totalAjustadaCdiBrl: number
  totalOperacoes: number
  porMes: Array<{ ano: number; mes: number; ajustadaCdiBrl: number }>
}

export function useEconomiaCdi(de: string, ate: string, bancoId?: string) {
  return useQuery({
    queryKey: ['cockpit', 'economia-cdi', de, ate, bancoId],
    queryFn: async (): Promise<EconomiaCockpit> => {
      // de/ate no formato "YYYY-MM" (ex.: "2026-01", "2026-05")
      const dto = await cotacoesApi.economia({ de, ate, bancoId })
      return {
        totalBrutaBrl: dto.totalEconomiaBrutaBrl,
        totalAjustadaCdiBrl: dto.totalEconomiaAjustadaCdiBrl,
        totalOperacoes: dto.totalOperacoes,
        porMes: dto.porMes.map((m) => ({
          ano: m.ano,
          mes: m.mes,
          ajustadaCdiBrl: m.economiaAjustadaCdiBrl,
        })),
      }
    },
    staleTime: 10 * 60_000, // 10 minutos
  })
}
```

### Exemplo de chamada

```
GET /api/v1/cotacoes/economia?de=2026-01&ate=2026-05
Authorization: Bearer <token>
```

Resposta:

```json
{
  "porMes": [
    { "ano": 2026, "mes": 1, "quantidadeOperacoes": 3, "economiaBrutaBrl": 45000.00, "economiaAjustadaCdiBrl": 38200.00 },
    { "ano": 2026, "mes": 2, "quantidadeOperacoes": 2, "economiaBrutaBrl": 28000.00, "economiaAjustadaCdiBrl": 23500.00 }
  ],
  "porBanco": [
    { "bancoId": "...", "quantidadeOperacoes": 5, "economiaBrutaBrl": 73000.00, "economiaAjustadaCdiBrl": 61700.00 }
  ],
  "totalEconomiaBrutaBrl": 73000.00,
  "totalEconomiaAjustadaCdiBrl": 61700.00,
  "totalOperacoes": 5
}
```

### Nota sobre o benchmark CDI

O campo `totalEconomiaAjustadaCdiBrl` equaliza operações de prazo diferente pelo CDI. Esta é a métrica recomendada para o card "Economia Negociada" do cockpit CFO.

**Limitação — Real Gap para SOFR/Selic (Task 4.4):** o endpoint atual calcula apenas a equalização por CDI. Os benchmarks SOFR (para captações Lei 4131 em USD/EUR) e Selic (para comparação com captações de mercado doméstico) não estão disponíveis e entram como Real Gap na Fase 4. Até a Task 4.4 estar pronta, o cockpit exibe apenas o comparativo CDI.

---

## GAP-CKP-19 — Spread Proposta × Contrato (parcial)

**Classificação:** Não-Gap para spread proposta-aceita × taxa contratada; Real Gap para spread completo com `TaxaIndicativaAa` (Task 4.10 futura)

### Endpoint disponível

Para detalhe de uma cotação:

```
GET /api/v1/cotacoes/{id}
Autorização: Leitura
```

Para comparativo estruturado de propostas:

```
GET /api/v1/cotacoes/{id}/comparativo
Autorização: Leitura
```

Parâmetro opcional: `aliquotaIrrfPercentual` (decimal) — relevante apenas para Lei 4131.

### Campos usados

**De `PropostaDto`** (dentro de `CotacaoDto.propostas[]`):

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `taxaAaPercentual` | decimal | Taxa nominal ao ano da proposta do banco |
| `spreadAaPercentual` | decimal | Spread sobre indexador (CDI/LIBOR) ao ano |
| `cetCalculadoAaPercentual` | decimal\|null | CET calculado automaticamente pelo backend |
| `valorTotalEstimadoBrl` | decimal\|null | Custo total estimado em BRL |
| `status` | string | `Recebida`, `Aceita`, `Recusada` |
| `bancoId` | guid | Banco que fez a proposta |

**De `ComparativoDto`** (via `GET /{id}/comparativo`):

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `taxaNominalAaPercentual` | decimal | `taxaAa + spreadAa` — o que o banco oferece nominalmente |
| `cetAaPercentual` | decimal | CET regulado — comparável entre propostas do mesmo prazo |
| `custoTotalEquivalenteBrl` | decimal | Custo equalizado via CDI — único ranking matemático puro entre prazos diferentes |
| `status` | string | Filtre por `Aceita` para exibir a proposta vencedora |

### Como implementar no FE

```typescript
// features/cockpit/composables/useSpreadNegociacao.ts
//
// Exibe: spread da proposta aceita e CET da proposta aceita × taxa do contrato final.
// Requer que a cotação esteja em status Convertida (contrato já criado).

import { useQuery } from '@tanstack/vue-query'
import { cotacoesApi } from '@/features/cotacoes/api/cotacoesApi'
import { contratosApi } from '@/features/contratos/api/contratosApi'

interface SpreadNegociacao {
  spreadPropostaAceitaAa: number        // SpreadAaPercentual da proposta vencedora
  taxaNominalPropostaAceitaAa: number   // TaxaAaPercentual da proposta vencedora
  cetPropostaAceitaAa: number | null    // CET calculado (pode ser null se não calculado)
  taxaContrato: number | null           // Taxa aa do contrato gerado (se disponível)
  diferencialBps: number | null         // (taxaContrato - taxaNominalPropostaAceitaAa) * 100
}

async function calcularSpread(cotacaoId: string): Promise<SpreadNegociacao> {
  const [cotacao, comparativo] = await Promise.all([
    cotacoesApi.getById(cotacaoId),
    cotacoesApi.comparativo(cotacaoId),
  ])

  const propostaAceita = cotacao.propostas.find((p) => p.status === 'Aceita')
  const comparativoAceito = comparativo.find((c) => c.status === 'Aceita')

  if (!propostaAceita || !comparativoAceito) {
    throw new Error('Nenhuma proposta aceita encontrada para esta cotação.')
  }

  // Taxa do contrato gerado (necessita de outra chamada se quiser comparar)
  let taxaContrato: number | null = null
  if (cotacao.contratoGeradoId) {
    const contrato = await contratosApi.getById(cotacao.contratoGeradoId)
    taxaContrato = contrato.taxaAaPercentual ?? null
  }

  const diferencialBps =
    taxaContrato !== null
      ? (taxaContrato - comparativoAceito.taxaNominalAaPercentual) * 100
      : null

  return {
    spreadPropostaAceitaAa: propostaAceita.spreadAaPercentual,
    taxaNominalPropostaAceitaAa: propostaAceita.taxaAaPercentual,
    cetPropostaAceitaAa: propostaAceita.cetCalculadoAaPercentual,
    taxaContrato,
    diferencialBps,
  }
}

export function useSpreadNegociacao(cotacaoId: string) {
  return useQuery({
    queryKey: ['cockpit', 'spread-negociacao', cotacaoId],
    queryFn: () => calcularSpread(cotacaoId),
    enabled: !!cotacaoId,
    staleTime: 15 * 60_000,
  })
}
```

### Nota sobre TaxaIndicativaAa (Task 4.10 futura)

O spread completo de negociação — definido como `taxa contratada − taxa indicativa de mercado na abertura` — exige o campo `TaxaIndicativaAa` na entidade `Cotacao` ou `Proposta`. **Este campo não existe no modelo atual.** Conforme decisão do sponsor de 2026-05-20 (§5 do plano), a adição de `TaxaIndicativaAa` entra como Task 4.10 na Fase 4 (P1).

Até a Task 4.10, o cockpit exibe "Spread proposta aceita: X% a.a." usando `SpreadAaPercentual` da `PropostaDto`. Este valor representa o spread sobre o indexador (CDI/SOFR) pactuado na proposta bancária — não a diferença em relação à taxa de mercado no momento da cotação.

---

## GAP-CKP-20 — Utilização de Limites por Banco (Headroom de Crédito)

**Classificação:** Não-Gap

### Endpoint disponível

```
GET /api/v1/limites-banco
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `bancoId` | guid | Filtra por banco específico |
| `modalidade` | string | Filtra por modalidade |

### Campos já disponíveis no `LimiteBancoDto`

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `bancoId` | guid | Identificador do banco |
| `modalidade` | string | Modalidade do limite |
| `valorLimiteBrl` | decimal | Limite total aprovado em BRL |
| `valorUtilizadoBrl` | decimal | Valor comprometido por contratos ativos em BRL |
| `valorDisponivelBrl` | decimal | `valorLimiteBrl − valorUtilizadoBrl` — calculado pelo backend |
| `dataVigenciaInicio` | date | Início da vigência do limite |
| `dataVigenciaFim` | date\|null | Fim da vigência (`null` = sem vencimento) |

> `valorDisponivelBrl` já é computado pelo backend. O FE não precisa subtrair os campos manualmente.

### Como somar no FE

O card "Headroom de Crédito" do cockpit do Gerente Financeiro exibe o saldo disponível consolidado de todos os bancos (ou por banco individualmente). A soma é feita client-side sobre a lista retornada pelo endpoint.

```typescript
// features/cockpit/composables/useHeadroomCredito.ts

import { useQuery } from '@tanstack/vue-query'
import { limitesBancoApi } from '@/features/limites-banco/api/limitesBancoApi'

interface HeadroomPorBanco {
  bancoId: string
  totalLimiteBrl: number
  totalUtilizadoBrl: number
  totalDisponivelBrl: number
  utilizacaoPct: number
}

interface HeadroomConsolidado {
  totalLimiteBrl: number
  totalUtilizadoBrl: number
  totalDisponivelBrl: number
  utilizacaoConsolidadaPct: number
  porBanco: HeadroomPorBanco[]
}

export function useHeadroomCredito() {
  return useQuery({
    queryKey: ['cockpit', 'headroom-credito'],
    queryFn: async (): Promise<HeadroomConsolidado> => {
      // Busca todos os limites ativos (sem filtro de banco ou modalidade)
      const limites = await limitesBancoApi.list()

      // Filtrar limites com vigência ativa (dataVigenciaFim nula ou futura)
      const hoje = new Date().toISOString().slice(0, 10) // YYYY-MM-DD
      const ativos = limites.filter(
        (l) => l.dataVigenciaFim === null || l.dataVigenciaFim >= hoje,
      )

      // Agrupar por banco (um banco pode ter limites por modalidade)
      const mapaBanco = new Map<string, { limite: number; utilizado: number; disponivel: number }>()
      for (const l of ativos) {
        const atual = mapaBanco.get(l.bancoId) ?? { limite: 0, utilizado: 0, disponivel: 0 }
        mapaBanco.set(l.bancoId, {
          limite: atual.limite + l.valorLimiteBrl,
          utilizado: atual.utilizado + l.valorUtilizadoBrl,
          disponivel: atual.disponivel + l.valorDisponivelBrl,
        })
      }

      const porBanco: HeadroomPorBanco[] = Array.from(mapaBanco.entries()).map(
        ([bancoId, vals]) => ({
          bancoId,
          totalLimiteBrl: vals.limite,
          totalUtilizadoBrl: vals.utilizado,
          totalDisponivelBrl: vals.disponivel,
          utilizacaoPct: vals.limite > 0 ? (vals.utilizado / vals.limite) * 100 : 0,
        }),
      )

      const totalLimiteBrl = porBanco.reduce((acc, b) => acc + b.totalLimiteBrl, 0)
      const totalUtilizadoBrl = porBanco.reduce((acc, b) => acc + b.totalUtilizadoBrl, 0)
      const totalDisponivelBrl = porBanco.reduce((acc, b) => acc + b.totalDisponivelBrl, 0)

      return {
        totalLimiteBrl,
        totalUtilizadoBrl,
        totalDisponivelBrl,
        utilizacaoConsolidadaPct: totalLimiteBrl > 0 ? (totalUtilizadoBrl / totalLimiteBrl) * 100 : 0,
        porBanco,
      }
    },
    staleTime: 5 * 60_000, // 5 minutos
  })
}
```

### Exemplo de chamada

```
GET /api/v1/limites-banco
Authorization: Bearer <token>
```

Resposta:

```json
[
  {
    "id": "3fa85f64-...",
    "bancoId": "abc123...",
    "modalidade": "Finimp",
    "valorLimiteBrl": 20000000.00,
    "valorUtilizadoBrl": 14500000.00,
    "valorDisponivelBrl": 5500000.00,
    "dataVigenciaInicio": "2026-01-01",
    "dataVigenciaFim": null,
    ...
  }
]
```

**Wireframe ASCII — Card Headroom:**

```
┌──────────────────────────────────────────┐
│  Headroom de Crédito                     │
│  ────────────────────────────────────── │
│  Limite Total:    R$ 80.000.000          │
│  Utilizado:       R$ 58.500.000 (73,1%)  │
│  Disponível:      R$ 21.500.000          │
│                                          │
│  Por Banco:                              │
│  Bradesco  ████████░░░░  R$ 8,5M / 12M  │
│  Itaú      ██████░░░░░░  R$ 7,0M / 11M  │
│  CEF       █████████░░░  R$ 6,0M / 7M   │
└──────────────────────────────────────────┘
```

> **Nota sobre o campo `RegraLimiteBancoUtilizacao`:** a Task 0.4 do plano de backend cria uma regra no rules engine que dispara um `Alerta` quando `valorUtilizadoBrl / valorLimiteBrl > 0.85`. Quando os alertas estiverem disponíveis (após Task 0.3/0.4), o card pode exibir o ícone de atenção diretamente da lista de alertas em vez de recalcular o percentual no FE.

---

## Resumo das chamadas de API por componente do cockpit

| Componente | Endpoint(s) | Frequência recomendada |
|------------|-------------|------------------------|
| Funil de cotações | `GET /api/v1/cotacoes?pageSize=100` (+ paginação) | Refetch on focus + manual |
| Cards a vencer | `GET /api/v1/contratos?status=Ativo&vencDe=...&vencAte=...` (×4 paralelo) | 5 min stale |
| Economia CDI | `GET /api/v1/cotacoes/economia?de=YYYY-MM&ate=YYYY-MM` | 10 min stale |
| Spread proposta × contrato | `GET /api/v1/cotacoes/{id}` + `GET /api/v1/cotacoes/{id}/comparativo` | 15 min stale |
| Headroom de crédito | `GET /api/v1/limites-banco` | 5 min stale |

---

## Referências

- `tasks/plan_cockpit_backend_gaps.md` §1.1 — classificação dos 26 gaps
- `docs/specs/cockpit/SPEC_0_5_status_cotacao.md` — expansão do `StatusCotacao` (Task 0.5)
- `src/Sgcf.Application/Cotacoes/CotacaoDto.cs` — campos disponíveis em `CotacaoDto`
- `src/Sgcf.Application/Cotacoes/PropostaDto.cs` — campos disponíveis em `PropostaDto`
- `src/Sgcf.Application/Cotacoes/LimiteBancoDto.cs` — campos disponíveis em `LimiteBancoDto`
- `src/Sgcf.Application/Cotacoes/EconomiaPeriodoDto.cs` — campos disponíveis em `EconomiaPeriodoDto`
- `src/Sgcf.Application/Cotacoes/ComparativoDto.cs` — campos disponíveis em `ComparativoDto`
- `docs/api/cotacoes.md` — documentação REST das cotações
- `docs/api/limites-banco.md` — documentação REST dos limites de banco
- `docs/api/contratos.md` — documentação REST dos contratos

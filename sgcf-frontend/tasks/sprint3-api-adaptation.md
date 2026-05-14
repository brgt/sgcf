# SGCF Frontend — Prompt de Adaptação Sprint 3 (API v0.4.0)

## Missão

O backend da SGCF evoluiu para a versão **v0.4.0** (Sprint 3). Este prompt descreve todas as adaptações necessárias no frontend Vue 3 para consumir os novos endpoints, tipos e enums. Execute cada tarefa exatamente como descrita, na ordem indicada, sem expandir escopo.

---

## Contexto do Projeto Frontend

- **Diretório raiz:** `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-frontend`
- **Stack:** Vue 3 + TypeScript strict (`exactOptionalPropertyTypes: true`, `noUncheckedIndexedAccess: true`), Vite 5, Pinia, TanStack Vue Query v5, Vue Router 4
- **Design System:** `@nordware/design-system` (local file dep — já instalado)
- **Padrões de API:** `apiClient` (GET), `postIdempotent` (POST de criação), `apiClient.patch`, `apiClient.delete` — todos importados de `@/shared/api/client`

---

## Leitura Obrigatória Antes de Implementar

Leia os seguintes arquivos **antes** de qualquer edição. Eles são a fonte de verdade da API:

| Arquivo | Conteúdo |
|---------|----------|
| `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/docs/changelog/CHANGELOG.md` | Changelog machine-readable — **leia primeiro** |
| `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/docs/api/schemas.md` | Todos os DTOs e enums |
| `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/docs/api/contratos.md` | Endpoints de contratos incl. PATCH |
| `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/docs/api/feriados.md` | Endpoints de feriados (GET/POST/DELETE) |
| `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/docs/api/auditoria.md` | Endpoint de auditoria (GET paginado) |
| `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/docs/api/plano-contas.md` | Endpoints de lançamentos contábeis |

Leia também os arquivos existentes do frontend antes de editar:

| Arquivo frontend | Por quê ler |
|-----------------|-------------|
| `src/shared/api/types.ts` | Interfaces TypeScript existentes |
| `src/shared/api/enums.ts` | Enums existentes — não duplicar |
| `src/shared/api/endpoints.ts` | Constantes de URL — padrão a seguir |
| `src/shared/api/client.ts` | Como fazer chamadas HTTP |
| `src/app/router.ts` | Rotas existentes — padrão a seguir |
| `src/layouts/components/SidebarNav.vue` | Nav existente — onde adicionar itens |
| `src/features/contratos/pages/ContratoCreatePage.vue` | Padrão de formulário multi-step |
| `src/features/contratos/pages/ContratoDetailPage.vue` | Padrão de página de detalhe |
| `src/features/plano-contas/pages/PlanoContasListPage.vue` | Padrão de listagem com ações inline |

---

## O Que Mudou no Backend (v0.4.0)

### 1. `ContratoDto` — 8 campos novos

Os seguintes campos foram adicionados à resposta de `GET /api/v1/contratos/{id}` e `POST /api/v1/contratos`:

| Campo | Tipo TS | Default da API |
|-------|---------|----------------|
| `periodicidade` | `Periodicidade` | `'Bullet'` |
| `estruturaAmortizacao` | `EstruturaAmortizacao` | `'Bullet'` |
| `quantidadeParcelas` | `number` | `1` |
| `dataPrimeiroVencimento` | `string` (YYYY-MM-DD) | `dataVencimento` |
| `anchorDiaMes` | `AnchorDiaMes` | `'DiaContratacao'` |
| `anchorDiaFixo` | `number \| null` | `null` |
| `periodicidadeJuros` | `Periodicidade \| null` | `null` |
| `convencaoDataNaoUtil` | `ConvencaoDataNaoUtil` | `'Following'` |

### 2. Novo endpoint: `PATCH /api/v1/contratos/{id}`

Atualização parcial de contrato. Somente campos não-nulos são aplicados. Política: `Escrita`.

### 3. Novo grupo: Feriados (`/api/v1/feriados`)

- `GET /api/v1/feriados?ano={ano}&escopo={escopo}` — Política: Leitura
- `POST /api/v1/feriados` — Política: Admin
- `DELETE /api/v1/feriados/{id}` — Política: Admin

### 4. Novo grupo: Lançamentos Contábeis (`/api/v1/plano-contas/{contaId}/lancamentos`)

- `POST /api/v1/plano-contas/{contaId}/lancamentos` — Política: Escrita
- `GET /api/v1/plano-contas/{contaId}/lancamentos` — Política: Auditoria

### 5. Novo endpoint: Log de Auditoria (`/audit/eventos`)

- `GET /audit/eventos` — Paginado, multi-filtro. Política: Auditoria.
- **Nota:** o base path é `/audit/` (sem `/api/v1/`).

### 6. Novos enums

`Periodicidade`, `EstruturaAmortizacao`, `AnchorDiaMes`, `ConvencaoDataNaoUtil`, `EscopoFeriado`, `TipoFeriado`, `FonteFeriado`

### 7. StatusContrato — 2 valores novos

`RefinanciadoParcial` e `RefinanciadoTotal` já estão em `enums.ts` (verifique antes de adicionar).

---

## Regras Invariantes — Nunca Viole

1. Nunca use `// @ts-ignore` ou `// @ts-expect-error`. Corrija a causa raiz.
2. Nunca instale dependências npm novas.
3. Nunca crie arquivos utilitários, composables ou helpers além do que este prompt indica.
4. Nunca refatore código fora da lista de tarefas.
5. Nunca passe `string | undefined` para prop `error` do DS. Use `|| ''`.
6. Nunca use `DateTime.now` ou strings de data sem o formato `YYYY-MM-DD`.
7. Sempre leia um arquivo antes de editá-lo.
8. Prefira `Edit` (diff) a `Write` (reescrita total) em arquivos existentes.
9. Não toque nas tarefas de alinhamento ao Design System já planejadas em `tasks/todo.md`. Esse trabalho é independente e já está rastreado.
10. Execute `npm test` e `npm run build` apenas uma vez, ao final de todas as tarefas.

---

## Padrões Estabelecidos — Siga Exatamente

### Chamadas de API

```typescript
import { apiClient, postIdempotent, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'

// GET
const { data } = await apiClient.get<ResponseType>(API.recurso.list, { params })

// POST de criação (com idempotência)
const { data } = await postIdempotent<ResponseType>(API.recurso.create, body)

// POST simples (sem idempotência)
const { data } = await apiClient.post<ResponseType>(API.recurso.action(id), body)

// PATCH
const { data } = await apiClient.patch<ResponseType>(API.recurso.update(id), body)

// DELETE
await apiClient.delete(API.recurso.delete(id))
```

### Enums TypeScript (padrão do arquivo `enums.ts`)

```typescript
export const NomeEnum = {
  ValorA: 'ValorA',
  ValorB: 'ValorB',
} as const
export type NomeEnum = (typeof NomeEnum)[keyof typeof NomeEnum]
```

### Prop `error` do DS sob TypeScript strict

```typescript
:error="(touched && errors['campo']) || ''"
// ou
:error="erroReativo || ''"
```

### Toast

```typescript
import { toast } from '@/shared/ui/toast'
toast.success('Operação realizada com sucesso.')
toast.error('Ocorreu um erro. Tente novamente.')
```

### Componentes DS disponíveis

`Input`, `Select`, `Checkbox`, `Textarea`, `Button`, `Badge`, `Alert`, `Card`, `Modal`, `DataTable`, `Pagination`, `Tabs`, `Dropdown`, `PageLayout`, `PageHeader`, `DashboardGrid`, `EmptyState`, `Spinner`

---

## Tarefas — Execute Nesta Ordem

---

### TAREFA 1 — Adicionar novos enums em `src/shared/api/enums.ts`

Verifique se `RefinanciadoParcial` e `RefinanciadoTotal` já existem no `StatusContrato`. Em seguida, **acrescente ao final do arquivo** os 7 novos enums:

```typescript
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
```

**Verificação:** `grep -n "Periodicidade\|EstruturaAmortizacao\|EscopoFeriado" src/shared/api/enums.ts` deve retornar as linhas adicionadas.

---

### TAREFA 2 — Atualizar `src/shared/api/types.ts`

#### 2a. Adicione os novos enums ao bloco de imports no topo do arquivo

```typescript
import type {
  // ... existentes ...
  Periodicidade,
  EstruturaAmortizacao,
  AnchorDiaMes,
  ConvencaoDataNaoUtil,
  EscopoFeriado,
  TipoFeriado,
  FonteFeriado,
} from './enums'
```

#### 2b. Atualize `ContratoDto` — adicione os 8 campos novos após `baseCalculo`

```typescript
  baseCalculo: BaseCalculo
  // --- campos de amortização (Sprint 3) ---
  periodicidade: Periodicidade
  estruturaAmortizacao: EstruturaAmortizacao
  quantidadeParcelas: number
  /** yyyy-MM-dd */
  dataPrimeiroVencimento: string
  anchorDiaMes: AnchorDiaMes
  anchorDiaFixo: number | null
  periodicidadeJuros: Periodicidade | null
  convencaoDataNaoUtil: ConvencaoDataNaoUtil
  // -----------------------------------------
  status: StatusContrato
```

#### 2c. Adicione as novas interfaces ao final da seção de CONTRATOS

```typescript
export interface UpdateContratoRequest {
  numeroExterno?: string | null
  taxaAa?: number | null
  dataVencimento?: string | null
  observacoes?: string | null
  baseCalculo?: BaseCalculo | null
  periodicidade?: Periodicidade | null
  estruturaAmortizacao?: EstruturaAmortizacao | null
  quantidadeParcelas?: number | null
  dataPrimeiroVencimento?: string | null
  convencaoDataNaoUtil?: ConvencaoDataNaoUtil | null
}
```

#### 2d. Adicione a seção FERIADOS ao final do arquivo

```typescript
// ============================================================================
// FERIADOS
// ============================================================================

export interface FeriadoDto {
  id: string
  /** yyyy-MM-dd */
  data: string
  descricao: string
  abrangencia: EscopoFeriado
  tipo: TipoFeriado
  fonte: FonteFeriado
  /** ISO 8601 instant */
  createdAt: string
}

export interface CreateFeriadoRequest {
  /** yyyy-MM-dd */
  data: string
  descricao: string
  abrangencia: EscopoFeriado
  tipo: TipoFeriado
}

// ============================================================================
// LANÇAMENTOS CONTÁBEIS
// ============================================================================

export interface LancamentoContabilDto {
  id: string
  contratoId: string
  planoContaId: string
  /** yyyy-MM-dd */
  data: string
  origem: string
  valor: number
  moeda: string
  descricao: string
  /** ISO 8601 instant */
  createdAt: string
}

export interface CreateLancamentoRequest {
  contratoId: string
  /** yyyy-MM-dd */
  data: string
  /** Máx. 50 caracteres */
  origem: string
  valorDecimal: number
  moeda: Moeda
  descricao: string
}

// ============================================================================
// AUDITORIA
// ============================================================================

export interface AuditEventoDto {
  id: number
  occurredAt: string
  actorSub: string
  actorRole: string
  source: 'rest' | 'mcp' | 'a2a' | 'job'
  entity: string
  entityId: string | null
  operation: 'CREATE' | 'UPDATE' | 'DELETE'
  diffJson: string | null
  requestId: string
}

export interface AuditFilter {
  entity?: string
  entityId?: string
  actorSub?: string
  source?: 'rest' | 'mcp' | 'a2a' | 'job'
  operation?: 'CREATE' | 'UPDATE' | 'DELETE'
  /** ISO 8601 */
  de?: string
  /** ISO 8601 */
  ate?: string
  page?: number
  pageSize?: number
}
```

---

### TAREFA 3 — Atualizar `src/shared/api/endpoints.ts`

Acrescente as entradas que faltam. Leia o arquivo inteiro primeiro para não duplicar.

```typescript
  contratos: {
    // ... existentes ...
    update: (id: string) => `/api/v1/contratos/${id}`,          // PATCH — novo
    hedges:             (id: string) => `/api/v1/contratos/${id}/hedges`,
  },

  feriados: {
    list:   '/api/v1/feriados',
    create: '/api/v1/feriados',
    delete: (id: string) => `/api/v1/feriados/${id}`,
  },

  planoContas: {
    // ... existentes ...
    lancamentos:       (contaId: string) => `/api/v1/plano-contas/${contaId}/lancamentos`,
  },

  auditoria: {
    eventos: '/audit/eventos',
  },
```

> **Atenção:** `auditoria.eventos` usa `/audit/` (sem `/api/v1/`). Verifique se o proxy Vite cobre esse prefixo. Se o arquivo `vite.config.ts` só proxia `/api`, adicione `/audit` como segundo target apontando para o mesmo backend.

---

### TAREFA 4 — Atualizar `src/app/router.ts`

Adicione duas novas rotas dentro do bloco de `children` do `AppShell`:

```typescript
{ path: 'feriados',  component: () => import('../features/feriados/pages/FeriadosListPage.vue'),  meta: { policy: 'Admin',      title: 'Feriados' } },
{ path: 'auditoria', component: () => import('../features/auditoria/pages/AuditoriaPage.vue'),    meta: { policy: 'Auditoria',  title: 'Auditoria' } },
```

---

### TAREFA 5 — Atualizar `src/layouts/components/SidebarNav.vue`

Leia o arquivo antes de editar. Identifique o padrão de item de navegação existente e adicione dois novos itens ao grupo de Administração / Configuração:

- **Feriados** — ícone sugerido: `i-carbon-calendar`, rota `/feriados`, requer policy `Admin`
- **Auditoria** — ícone sugerido: `i-carbon-activity`, rota `/auditoria`, requer policy `Auditoria`

Siga exatamente o mesmo padrão de objeto/componente usado pelos itens existentes. Não invente novos padrões.

---

### TAREFA 6 — Atualizar `src/features/contratos/pages/ContratoCreatePage.vue`

Leia o arquivo inteiro antes de editar. Identifique a etapa do formulário que contém `dataVencimento` e `baseCalculo`. Após esses campos, adicione os campos de amortização:

```html
<!-- Periodicidade -->
<Select
  v-model="form.periodicidade"
  label="Periodicidade"
  :options="periodicidadeOptions"
/>

<!-- Estrutura de Amortização -->
<Select
  v-model="form.estruturaAmortizacao"
  label="Estrutura de Amortização"
  :options="estruturaAmortizacaoOptions"
/>

<!-- Quantidade de Parcelas -->
<Input
  v-model="form.quantidadeParcelas"
  type="number"
  label="Quantidade de Parcelas"
  :error="errors.quantidadeParcelas || ''"
/>

<!-- Data do Primeiro Vencimento -->
<div class="native-date-field">
  <label>Data do Primeiro Vencimento</label>
  <input v-model="form.dataPrimeiroVencimento" type="date" />
</div>

<!-- Âncora do Dia de Vencimento -->
<Select
  v-model="form.anchorDiaMes"
  label="Âncora do Dia de Vencimento"
  :options="anchorDiaMesOptions"
/>

<!-- Dia Fixo (condicional) -->
<Input
  v-if="form.anchorDiaMes === 'DiaFixo'"
  v-model="form.anchorDiaFixo"
  type="number"
  label="Dia Fixo (1–31)"
  :error="errors.anchorDiaFixo || ''"
/>

<!-- Convenção Data Não Útil -->
<Select
  v-model="form.convencaoDataNaoUtil"
  label="Convenção de Data Não Útil"
  :options="convencaoDataNaoUtilOptions"
/>
```

**No `<script setup>`**, defina os arrays de opções para os `<Select>`:

```typescript
import { Periodicidade, EstruturaAmortizacao, AnchorDiaMes, ConvencaoDataNaoUtil } from '@/shared/api/enums'

const periodicidadeOptions = Object.values(Periodicidade).map(v => ({ label: v, value: v }))
const estruturaAmortizacaoOptions = Object.values(EstruturaAmortizacao).map(v => ({ label: v, value: v }))
const anchorDiaMesOptions = Object.values(AnchorDiaMes).map(v => ({ label: v, value: v }))
const convencaoDataNaoUtilOptions = Object.values(ConvencaoDataNaoUtil).map(v => ({ label: v, value: v }))
```

**Adicione os campos ao objeto `form`** com os valores default da API:

```typescript
periodicidade: Periodicidade.Bullet as Periodicidade,
estruturaAmortizacao: EstruturaAmortizacao.Bullet as EstruturaAmortizacao,
quantidadeParcelas: 1,
dataPrimeiroVencimento: '',
anchorDiaMes: AnchorDiaMes.DiaContratacao as AnchorDiaMes,
anchorDiaFixo: null as number | null,
convencaoDataNaoUtil: ConvencaoDataNaoUtil.Following as ConvencaoDataNaoUtil,
```

**Inclua os campos no payload enviado ao backend** (no `POST /api/v1/contratos`). Omita `anchorDiaFixo` quando for `null`.

---

### TAREFA 7 — Atualizar `src/features/contratos/pages/ContratoDetailPage.vue`

#### 7a. Exibir os 8 novos campos na seção de detalhes

Leia o arquivo. Identifique onde `baseCalculo` e `taxaAa` são exibidos. Após essa seção, adicione um bloco "Amortização":

```html
<Card title="Amortização">
  <dl>
    <dt>Periodicidade</dt>           <dd>{{ contrato.periodicidade }}</dd>
    <dt>Estrutura</dt>               <dd>{{ contrato.estruturaAmortizacao }}</dd>
    <dt>Parcelas</dt>                <dd>{{ contrato.quantidadeParcelas }}</dd>
    <dt>1º Vencimento</dt>           <dd>{{ formatDate(contrato.dataPrimeiroVencimento) }}</dd>
    <dt>Âncora Dia</dt>              <dd>{{ contrato.anchorDiaMes }}</dd>
    <dt>Dia Fixo</dt>                <dd>{{ contrato.anchorDiaFixo ?? '—' }}</dd>
    <dt>Periodicidade Juros</dt>     <dd>{{ contrato.periodicidadeJuros ?? '—' }}</dd>
    <dt>Conv. Data Não Útil</dt>     <dd>{{ contrato.convencaoDataNaoUtil }}</dd>
  </dl>
</Card>
```

Use `formatDate` de `@/shared/dates/formatDate` (já importado ou importe se necessário).

#### 7b. Adicionar botão "Editar" e modal de edição via PATCH

Adicione um botão "Editar Contrato" na área de ações da página (junto ao botão Exportar existente). Ao clicar, abra um `<Modal>` com um formulário de edição parcial.

O formulário do modal deve conter apenas os campos mutáveis do PATCH:

```html
<Modal v-model="showEditModal" title="Editar Contrato" size="lg">
  <Input  v-model="editForm.numeroExterno"  label="Número Externo" full-width />
  <Input  v-model="editForm.taxaAa"         type="number" label="Taxa a.a. (%)" />
  <div class="native-date-field">
    <label>Data de Vencimento</label>
    <input v-model="editForm.dataVencimento" type="date" />
  </div>
  <Select v-model="editForm.baseCalculo"    label="Base de Cálculo"   :options="baseCalculoOptions" />
  <Select v-model="editForm.periodicidade"  label="Periodicidade"     :options="periodicidadeOptions" />
  <Select v-model="editForm.estruturaAmortizacao" label="Estrutura"   :options="estruturaOptions" />
  <Input  v-model="editForm.quantidadeParcelas" type="number" label="Qtd. Parcelas" />
  <Select v-model="editForm.convencaoDataNaoUtil" label="Conv. Data Não Útil" :options="convencaoOptions" />
  <Textarea v-model="editForm.observacoes" label="Observações" :rows="3" />

  <template #footer>
    <Button variant="secondary" @click="showEditModal = false">Cancelar</Button>
    <Button variant="primary" :loading="isSaving" @click="salvarEdicao">Salvar</Button>
  </template>
</Modal>
```

**Função `salvarEdicao`** — envie apenas os campos com valor não-nulo / não-vazio:

```typescript
async function salvarEdicao() {
  isSaving.value = true
  try {
    const payload: UpdateContratoRequest = {}
    if (editForm.value.numeroExterno)        payload.numeroExterno = editForm.value.numeroExterno
    if (editForm.value.taxaAa != null)       payload.taxaAa = editForm.value.taxaAa
    if (editForm.value.dataVencimento)       payload.dataVencimento = editForm.value.dataVencimento
    if (editForm.value.observacoes != null)  payload.observacoes = editForm.value.observacoes
    if (editForm.value.baseCalculo)          payload.baseCalculo = editForm.value.baseCalculo
    if (editForm.value.periodicidade)        payload.periodicidade = editForm.value.periodicidade
    if (editForm.value.estruturaAmortizacao) payload.estruturaAmortizacao = editForm.value.estruturaAmortizacao
    if (editForm.value.quantidadeParcelas != null) payload.quantidadeParcelas = editForm.value.quantidadeParcelas
    if (editForm.value.convencaoDataNaoUtil) payload.convencaoDataNaoUtil = editForm.value.convencaoDataNaoUtil

    await apiClient.patch<ContratoDto>(API.contratos.update(contratoId), payload)
    await queryClient.invalidateQueries({ queryKey: ['contratos', contratoId] })
    showEditModal.value = false
    toast.success('Contrato atualizado com sucesso.')
  } catch (err) {
    toast.error(extractApiError(err))
  } finally {
    isSaving.value = false
  }
}
```

Import necessário: `import type { UpdateContratoRequest } from '@/shared/api/types'`

---

### TAREFA 8 — Atualizar `src/features/contratos/components/ContratoFilterPanel.vue`

Leia o arquivo. Identifique o `<Select>` de status. Verifique se `RefinanciadoParcial` e `RefinanciadoTotal` já estão nas opções. Se não estiverem, adicione-os à lista de opções do filtro de status.

---

### TAREFA 9 — Criar `src/features/feriados/`

Crie a estrutura:

```
src/features/feriados/
  api/
    useFeriados.ts
  pages/
    FeriadosListPage.vue
```

#### `src/features/feriados/api/useFeriados.ts`

```typescript
import { apiClient, postIdempotent, extractApiError } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import type { FeriadoDto, CreateFeriadoRequest } from '@/shared/api/types'

export async function listFeriados(ano?: number, escopo?: string): Promise<FeriadoDto[]> {
  const params: Record<string, string | number> = {}
  if (ano) params['ano'] = ano
  if (escopo) params['escopo'] = escopo
  const { data } = await apiClient.get<FeriadoDto[]>(API.feriados.list, { params })
  return data
}

export async function createFeriado(body: CreateFeriadoRequest): Promise<FeriadoDto> {
  const { data } = await apiClient.post<FeriadoDto>(API.feriados.create, body)
  return data
}

export async function deleteFeriado(id: string): Promise<void> {
  await apiClient.delete(API.feriados.delete(id))
}
```

#### `src/features/feriados/pages/FeriadosListPage.vue`

A página deve ter:

1. **Filtros** — `<Input type="number">` para `ano` (default: ano corrente) e `<Select>` para `escopo` (Nacional/Estadual/Municipal/Todos)
2. **Tabela** (`<DataTable>`) com colunas: `data`, `descricao`, `abrangencia`, `tipo`, `fonte`
3. **Botão "Adicionar Feriado"** — visível apenas para usuários com policy `Admin` (use `useAuth` / `hasPolicy`). Abre um `<Modal>` com o formulário de criação.
4. **Botão "Excluir"** por linha — visível apenas para feriados com `fonte === 'Manual'` e policy `Admin`. Use `useConfirm` para confirmação antes de chamar `deleteFeriado`.
5. **Toast** de sucesso/erro em todas as ações.

**Formulário de criação** (dentro do Modal):

```html
<div class="native-date-field">
  <label>Data *</label>
  <input v-model="createForm.data" type="date" required />
</div>
<Input v-model="createForm.descricao" label="Descrição" required full-width />
<Select v-model="createForm.abrangencia" label="Abrangência" required :options="escopoOptions" />
<Select v-model="createForm.tipo"        label="Tipo"        required :options="tipoOptions" />
```

Padrão de `<script setup>`:

```typescript
import { ref, computed } from 'vue'
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { listFeriados, createFeriado, deleteFeriado } from '../api/useFeriados'
import { EscopoFeriado, TipoFeriado, FonteFeriado } from '@/shared/api/enums'
import type { CreateFeriadoRequest } from '@/shared/api/types'
import { toast } from '@/shared/ui/toast'
import { useConfirm } from '@/shared/ui/useConfirm'
import { useAuthStore } from '@/shared/auth/useAuth'

const auth = useAuthStore()
const queryClient = useQueryClient()
const { confirm } = useConfirm()

const anoFiltro = ref(new Date().getFullYear())
const escopoFiltro = ref('')

const { data: feriados, isLoading } = useQuery({
  queryKey: computed(() => ['feriados', anoFiltro.value, escopoFiltro.value]),
  queryFn: () => listFeriados(anoFiltro.value || undefined, escopoFiltro.value || undefined),
})

const escopoOptions = Object.values(EscopoFeriado).map(v => ({ label: v, value: v }))
const tipoOptions   = Object.values(TipoFeriado).map(v => ({ label: v, value: v }))
```

---

### TAREFA 10 — Atualizar `src/features/plano-contas/`

#### 10a. Crie `src/features/plano-contas/api/useLancamentos.ts`

```typescript
import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import type { LancamentoContabilDto, CreateLancamentoRequest } from '@/shared/api/types'

export async function getLancamentos(contaId: string): Promise<LancamentoContabilDto[]> {
  const { data } = await apiClient.get<LancamentoContabilDto[]>(API.planoContas.lancamentos(contaId))
  return data
}

export async function createLancamento(contaId: string, body: CreateLancamentoRequest): Promise<LancamentoContabilDto> {
  const { data } = await apiClient.post<LancamentoContabilDto>(API.planoContas.lancamentos(contaId), body)
  return data
}
```

#### 10b. Atualizar `src/features/plano-contas/pages/PlanoContasListPage.vue`

Leia o arquivo inteiro. Adicione uma ação por linha na tabela: botão ou link "Lançamentos" que navega para uma sub-rota ou abre um `<Modal>` exibindo os lançamentos da conta.

**Opção recomendada — Modal de Lançamentos:**

Ao clicar em "Lançamentos" de uma conta, abra um `<Modal size="lg">` que:
1. Busca os lançamentos via `useQuery` com `queryKey: ['lancamentos', contaId]`
2. Exibe em `<DataTable>` com colunas: `data`, `origem`, `valor`, `moeda`, `descricao`, `contratoId` (truncado)
3. Exibe botão "Novo Lançamento" que abre um segundo modal de criação

**Formulário de criação de lançamento:**

```html
<Input v-model="lancForm.contratoId" label="ID do Contrato" full-width :error="lancErrors.contratoId || ''" />
<div class="native-date-field">
  <label>Data *</label>
  <input v-model="lancForm.data" type="date" required />
</div>
<Input v-model="lancForm.origem"      label="Origem (máx. 50 chars)" full-width :error="lancErrors.origem || ''" />
<Input v-model="lancForm.valorDecimal" type="number" label="Valor" :error="lancErrors.valorDecimal || ''" />
<Select v-model="lancForm.moeda"      label="Moeda" :options="moedaOptions" />
<Input v-model="lancForm.descricao"   label="Descrição" full-width :error="lancErrors.descricao || ''" />
```

---

### TAREFA 11 — Criar `src/features/auditoria/`

Crie:

```
src/features/auditoria/
  api/
    useAuditoria.ts
  pages/
    AuditoriaPage.vue
```

#### `src/features/auditoria/api/useAuditoria.ts`

```typescript
import { apiClient } from '@/shared/api/client'
import { API } from '@/shared/api/endpoints'
import type { AuditEventoDto, AuditFilter, PagedResult } from '@/shared/api/types'

export async function listEventos(filter: AuditFilter): Promise<PagedResult<AuditEventoDto>> {
  const params: Record<string, string | number> = {}
  if (filter.entity)    params['entity']    = filter.entity
  if (filter.entityId)  params['entityId']  = filter.entityId
  if (filter.actorSub)  params['actorSub']  = filter.actorSub
  if (filter.source)    params['source']    = filter.source
  if (filter.operation) params['operation'] = filter.operation
  if (filter.de)        params['de']        = filter.de
  if (filter.ate)       params['ate']       = filter.ate
  params['page']     = filter.page     ?? 1
  params['pageSize'] = filter.pageSize ?? 50

  const { data } = await apiClient.get<PagedResult<AuditEventoDto>>(API.auditoria.eventos, { params })
  return data
}
```

#### `src/features/auditoria/pages/AuditoriaPage.vue`

A página deve ter:

1. **Filtros** em linha:
   - `<Input>` para `entity` (ex.: Contrato, Banco)
   - `<Select>` para `source` (`rest | mcp | a2a | job | todos`)
   - `<Select>` para `operation` (`CREATE | UPDATE | DELETE | todos`)
   - Dois `<input type="date">` nativos para `de` e `ate`
   - `<Input>` para `actorSub`
   - Botão "Filtrar"

2. **Tabela** (`<DataTable>`) com colunas:
   - `occurredAt` — formatado como data+hora BR via `formatInstant` de `@/shared/dates/formatInstant`
   - `entity` + `operation` — use `<Badge>` com variant adequado (CREATE→success, UPDATE→info, DELETE→danger)
   - `actorSub`
   - `source`
   - `entityId` — truncado (8 chars) com tooltip completo

3. **Detalhe do diff** — ao clicar em uma linha, abra um `<Modal>` exibindo o `diffJson` formatado (use `JSON.parse` + `JSON.stringify(obj, null, 2)` dentro de um `<pre>`)

4. **Paginação** — use `<Pagination>` do DS. `pageSize` fixo em 50.

---

### TAREFA 12 — Verificar proxy Vite para `/audit`

Abra `vite.config.ts`. Se o proxy só cobre `/api`, adicione:

```typescript
'/audit': {
  target: 'http://localhost:5000',
  changeOrigin: true,
},
```

Isso garante que `GET /audit/eventos` chegue ao backend.

---

## Checklist de Verificação (executar ao final)

Execute cada passo e confirme o resultado esperado:

### 1. TypeScript sem erros

```bash
npm run build
```

Esperado: exit code 0, zero erros de TypeScript.

### 2. Testes

```bash
npm test
```

Esperado: todos os testes existentes passam. Não quebre testes existentes.

### 3. Novos enums presentes

```bash
grep -n "Periodicidade\|EstruturaAmortizacao\|AnchorDiaMes\|ConvencaoDataNaoUtil\|EscopoFeriado\|TipoFeriado\|FonteFeriado" src/shared/api/enums.ts
```

Esperado: 7 blocos de enum presentes.

### 4. Novos tipos presentes

```bash
grep -n "FeriadoDto\|LancamentoContabilDto\|AuditEventoDto\|UpdateContratoRequest" src/shared/api/types.ts
```

Esperado: 4 matches.

### 5. Endpoints registrados

```bash
grep -n "feriados\|auditoria\|update.*contratos\|lancamentos" src/shared/api/endpoints.ts
```

Esperado: todas as entradas visíveis.

### 6. Rotas adicionadas

```bash
grep -n "feriados\|auditoria" src/app/router.ts
```

Esperado: 2 rotas.

### 7. Novos arquivos criados

```bash
find src/features/feriados src/features/auditoria -type f
```

Esperado: pelo menos 4 arquivos (2 por feature: `api/*.ts` + `pages/*.vue`).

### 8. `ContratoDto` atualizado

```bash
grep -n "periodicidade\|estruturaAmortizacao\|anchorDiaMes\|convencaoDataNaoUtil" src/shared/api/types.ts
```

Esperado: 8 campos presentes na interface.

---

## Relatório Final

Ao concluir, forneça:

1. Lista de arquivos **modificados** (caminhos absolutos).
2. Lista de arquivos **criados** (caminhos absolutos).
3. Resultado de `npm run build` (exit code e eventuais avisos).
4. Resultado de `npm test` (X testes passaram).
5. Saída de cada grep do checklist acima.

Nenhum outro comentário.

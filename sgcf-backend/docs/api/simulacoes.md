# Simulações API

**Base route:** `/api/v1/simulacoes`

Gerencia cenários de simulação de contratação: criação, edição, ativação, arquivamento e duplicação de cenários nomeados; adição e remoção de captações hipotéticas dentro de cada cenário; preview de cronograma hipotético stateless; e integração com o Quadro da Dívida para projetar o impacto das captações simuladas.

> **Referência:** SPEC `docs/specs/simulacoes/SPEC.md`. Decisões arquiteturais em `tasks/quadro-divida-simulacao/plan.md` §2.

---

## Ciclo de Vida do Cenário

```
Rascunho → Ativo → Arquivado
```

| De | Para | Comando | Política |
|----|------|---------|---------|
| `Rascunho` | `Ativo` | `POST /cenarios/{id}/ativar` | Escrita |
| `Ativo` | `Arquivado` | `POST /cenarios/{id}/arquivar` | Gerencial |
| `Rascunho` | `Arquivado` | `POST /cenarios/{id}/arquivar` | Gerencial |
| qualquer | cópia `Rascunho` | `POST /cenarios/{id}/duplicar` | Escrita |

**Regras de transição:**

- Um cenário `Arquivado` é imutável via API. Tentativas de editar campos ou adicionar simulações retornam `409 Conflict`.
- Apenas cenários em `Rascunho` ou `Ativo` aceitam edição de campos e adição/remoção de simulações filhas.
- `Arquivar` é irreversível via API. Cenários arquivados podem ser duplicados para criar novos rascunhos.
- Qualquer membro com política `Escrita` pode editar qualquer cenário — sem controle de owner (D-6).
- Operações de arquivamento exigem política `Gerencial` (equivalente ao papel Gerente — AD-11).

---

## Endpoints — Cronograma Hipotético (Preview)

### Preview de Cronograma

```
POST /api/v1/simulacoes/cronograma-hipotetico
Autorização: Escrita
```

Calcula o cronograma de amortização de uma operação hipotética **sem persistir nada**. Endpoint stateless: útil para o frontend mostrar o fluxo de caixa antes de salvar a simulação em um cenário.

Reutiliza o mesmo motor de cronograma (`CronogramaStrategyFactory`) usado em contratos reais. O resultado é idêntico ao que seria gerado se a operação fosse convertida em contrato.

**Request Body:**

```json
{
  "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "modalidade": "Finimp",
  "moeda": "USD",
  "valorPrincipal": 5000000.00,
  "dataContratacaoPrevista": "2026-07-01",
  "dataPrimeiroVencimento": "2026-08-01",
  "tipoTaxa": "Fixa",
  "taxaAa": 6.5,
  "spreadAa": null,
  "baseCalculo": "Dias360",
  "estruturaAmortizacao": "Bullet",
  "periodicidade": "Bullet",
  "quantidadeParcelas": 1,
  "anchorDiaMes": "DiaContratacao",
  "anchorDiaFixo": null,
  "garantiaExigidaPrevista": "CDB cativo 20% do principal",
  "observacoes": null,
  "cdiReferenciaAaPercentual": null
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `bancoId` | guid | Sim | Banco credor da operação hipotética |
| `modalidade` | string | Sim | Ver enum [ModalidadeContrato](./schemas.md#modalidadecontrato) |
| `moeda` | string | Sim | Ver enum [Moeda](./schemas.md#moeda) |
| `valorPrincipal` | decimal | Sim | Valor principal em moeda da operação (> 0) |
| `dataContratacaoPrevista` | date | Sim | Data prevista de contratação (`YYYY-MM-DD`) |
| `dataPrimeiroVencimento` | date | Sim | Data do primeiro vencimento (`YYYY-MM-DD`, posterior à contratação) |
| `tipoTaxa` | string | Sim | `Fixa` ou `CdiSpread` (ver [TipoTaxa](#tipotaxa)) |
| `taxaAa` | decimal? | Condicional | Taxa fixa anual em %. Obrigatório quando `tipoTaxa = Fixa` |
| `spreadAa` | decimal? | Condicional | Spread sobre CDI anual em %. Obrigatório quando `tipoTaxa = CdiSpread` |
| `baseCalculo` | string | Sim | `Dias252`, `Dias360` ou `Dias365` |
| `estruturaAmortizacao` | string | Sim | Ver [EstruturaAmortizacao](./schemas.md#estruturaamortizacao) |
| `periodicidade` | string | Sim | Ver [Periodicidade](./schemas.md#periodicidade) |
| `quantidadeParcelas` | int | Sim | Número de parcelas (≥ 1) |
| `anchorDiaMes` | string | Sim | Ver [AnchorDiaMes](./schemas.md#anchordiames) |
| `anchorDiaFixo` | int? | Condicional | Dia fixo (1–31). Obrigatório quando `anchorDiaMes = DiaFixo` |
| `garantiaExigidaPrevista` | string? | Não | Descrição textual da garantia esperada. Máx. 500 chars. Informativo (D-9) |
| `observacoes` | string? | Não | Texto livre |
| `cdiReferenciaAaPercentual` | decimal? | Condicional | CDI de referência anual em %. Obrigatório quando `tipoTaxa = CdiSpread` |

**Response 200 OK:**

```json
{
  "taxaEfetivaAaPercentual": 6.50,
  "quantidadeEventos": 1,
  "principalTotal": 5000000.00,
  "jurosTotal": 325000.00,
  "eventos": [
    {
      "numero": 1,
      "tipo": "Principal",
      "data": "2026-07-01",
      "valor": 5000000.00,
      "saldoDevedorApos": 0.00
    }
  ]
}
```

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `taxaEfetivaAaPercentual` | decimal | Taxa efetiva anual calculada |
| `quantidadeEventos` | int | Total de eventos no cronograma |
| `principalTotal` | decimal | Soma de todos os eventos de principal |
| `jurosTotal` | decimal | Soma de todos os eventos de juros |
| `eventos[]` | array | Lista de eventos do cronograma |
| `eventos[].numero` | int | Número sequencial do evento |
| `eventos[].tipo` | string | Tipo do evento (ex.: `Principal`, `Juros`) |
| `eventos[].data` | date | Data do evento (`YYYY-MM-DD`) |
| `eventos[].valor` | decimal | Valor do evento |
| `eventos[].saldoDevedorApos` | decimal? | Saldo devedor após o evento. `null` para eventos de juros puros |

**Erros:**
- `400 Bad Request` — Invariante de domínio violada (ex.: `valorPrincipal = 0`; `CdiSpread` sem `cdiReferenciaAaPercentual`)
- `409 Conflict` — Estado interno inválido (raro em uso normal)

---

## Endpoints — CRUD de Cenários

### Criar Cenário

```
POST /api/v1/simulacoes/cenarios
Autorização: Escrita
Idempotency-Key: suportado (TTL 24h)
```

Cria um novo cenário em status `Rascunho`.

**Request Body:**

```json
{
  "nome": "Realista 2026",
  "anoBase": 2026,
  "descricao": "Captações conservadoras para o segundo semestre"
}
```

| Campo | Tipo | Obrigatório | Validação |
|-------|------|-------------|-----------|
| `nome` | string | Sim | Não vazio, máx. 100 chars |
| `anoBase` | int | Sim | 2020–2050 |
| `descricao` | string? | Não | Texto livre |

**Response 201 Created:** `CenarioSimulacaoDto`

**Erros:**
- `400 Bad Request` — Validação de campo

---

### Listar Cenários

```
GET /api/v1/simulacoes/cenarios
Autorização: Leitura
```

Retorna lista resumida de cenários (sem simulações filhas). Cenários com soft-delete são excluídos automaticamente.

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `status` | string | Filtra por `Rascunho`, `Ativo` ou `Arquivado` |
| `anoBase` | int | Filtra por ano-base do cenário |
| `criadoPor` | string | Filtra por `actorSub` do criador |

**Response 200 OK:** `CenarioSimulacaoResumoDto[]`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nome": "Realista 2026",
    "status": "Rascunho",
    "anoBase": 2026,
    "qtdeSimulacoes": 2,
    "criadoPor": "tesouraria@empresa.com",
    "updatedAt": "2026-05-19T14:30:00Z"
  }
]
```

---

### Obter Cenário

```
GET /api/v1/simulacoes/cenarios/{id}
Autorização: Leitura
```

Retorna o cenário completo com todas as simulações filhas.

**Response 200 OK:** `CenarioSimulacaoDto`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nome": "Realista 2026",
  "descricao": "Captações conservadoras para o segundo semestre",
  "anoBase": 2026,
  "status": "Rascunho",
  "criadoPor": "tesouraria@empresa.com",
  "createdAt": "2026-05-19T14:00:00Z",
  "updatedAt": "2026-05-19T14:30:00Z",
  "simulacoes": [
    {
      "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "cenarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bancoId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "modalidade": "Finimp",
      "moeda": "USD",
      "valorPrincipal": 5000000.00,
      "dataContratacaoPrevista": "2026-07-01",
      "dataPrimeiroVencimento": "2027-07-01",
      "tipoTaxa": "Fixa",
      "taxaAa": 6.5,
      "spreadAa": null,
      "baseCalculo": "Dias360",
      "estruturaAmortizacao": "Bullet",
      "periodicidade": "Bullet",
      "quantidadeParcelas": 1,
      "anchorDiaMes": "DiaContratacao",
      "anchorDiaFixo": null,
      "garantiaExigidaPrevista": "CDB cativo 20% do principal",
      "observacoes": null,
      "version": 1,
      "createdAt": "2026-05-19T14:10:00Z",
      "updatedAt": "2026-05-19T14:10:00Z"
    }
  ]
}
```

**Erros:**
- `404 Not Found` — Cenário não encontrado ou deletado

---

### Atualizar Cenário

```
PATCH /api/v1/simulacoes/cenarios/{id}
Autorização: Escrita
```

Atualiza `nome`, `descricao` e/ou `anoBase` de um cenário. Permitido em `Rascunho` e `Ativo`; bloqueado em `Arquivado`.

A atualização é parcial: campos com valor `null` no body são ignorados (campo preservado).

**Request Body:**

```json
{
  "nome": "Realista 2026 — revisado",
  "descricao": "Premissas atualizadas após reunião de comitê",
  "anoBase": 2026
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `nome` | string? | Não | Novo nome. Quando informado, não pode ser vazio nem exceder 100 chars |
| `descricao` | string? | Não | Nova descrição |
| `anoBase` | int? | Não | Novo ano-base (2020–2050) |

**Response 200 OK:** `CenarioSimulacaoDto`

**Erros:**
- `400 Bad Request` — Validação de campo
- `404 Not Found` — Cenário não encontrado
- `409 Conflict` — Cenário está `Arquivado`

---

### Ativar Cenário

```
POST /api/v1/simulacoes/cenarios/{id}/ativar
Autorização: Escrita
```

Transita o cenário de `Rascunho` para `Ativo`.

**Response 200 OK:** `CenarioSimulacaoDto`

**Erros:**
- `404 Not Found` — Cenário não encontrado
- `409 Conflict` — Cenário não está em `Rascunho`

---

### Arquivar Cenário

```
POST /api/v1/simulacoes/cenarios/{id}/arquivar
Autorização: Gerencial
```

Arquiva um cenário `Ativo` ou `Rascunho`. Operação irreversível via API. O registro permanece no banco; cenários arquivados aparecem em listagens com `status = "Arquivado"` e podem ser duplicados.

> Exige política `Gerencial` (equivalente ao papel Gerente — AD-11).

**Response 200 OK:** `CenarioSimulacaoDto`

**Erros:**
- `404 Not Found` — Cenário não encontrado
- `409 Conflict` — Cenário já está `Arquivado`

---

### Duplicar Cenário

```
POST /api/v1/simulacoes/cenarios/{id}/duplicar
Autorização: Escrita
Idempotency-Key: suportado (TTL 24h)
```

Cria uma cópia profunda do cenário em status `Rascunho` com novo `Id`. Todas as simulações filhas são copiadas com novos `Id`s. O nome da cópia recebe o sufixo ` (cópia)`. Funciona em cenários de qualquer status, incluindo `Arquivado` (D-10 / Q7).

**Response 201 Created:** `CenarioSimulacaoDto`

**Erros:**
- `404 Not Found` — Cenário origem não encontrado

---

### Deletar Cenário

```
DELETE /api/v1/simulacoes/cenarios/{id}
Autorização: Escrita
```

Soft-delete do cenário. O registro permanece no banco com `deletadoEm` preenchido. Chamadas subsequentes de `GET` retornam `404`. Listagens excluem cenários deletados automaticamente via query filter.

**Response 204 No Content**

**Erros:**
- `404 Not Found` — Cenário não encontrado

---

## Endpoints — Simulações dentro do Cenário

### Adicionar Simulação

```
POST /api/v1/simulacoes/cenarios/{id}/simulacoes
Autorização: Escrita
Idempotency-Key: suportado (TTL 24h)
```

Adiciona uma nova captação hipotética ao cenário. Bloqueado se o cenário estiver `Arquivado`. Retorna o cenário completo atualizado.

**Request Body:** `AdicionarSimulacaoInput`

```json
{
  "bancoId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "modalidade": "Finimp",
  "moeda": "USD",
  "valorPrincipal": 5000000.00,
  "dataContratacaoPrevista": "2026-07-01",
  "dataPrimeiroVencimento": "2027-07-01",
  "tipoTaxa": "Fixa",
  "taxaAa": 6.5,
  "spreadAa": null,
  "baseCalculo": "Dias360",
  "estruturaAmortizacao": "Bullet",
  "periodicidade": "Bullet",
  "quantidadeParcelas": 1,
  "anchorDiaMes": "DiaContratacao",
  "anchorDiaFixo": null,
  "garantiaExigidaPrevista": "CDB cativo 20%",
  "observacoes": null
}
```

Os campos têm a mesma semântica dos campos correspondentes em [Preview de Cronograma](#preview-de-cronograma). Consulte a tabela de campos do endpoint de preview para validações detalhadas.

**Response 201 Created:** `CenarioSimulacaoDto` (cenário completo com a nova simulação)

**Erros:**
- `400 Bad Request` — Invariante de domínio violada
- `404 Not Found` — Cenário não encontrado
- `409 Conflict` — Cenário está `Arquivado`

---

### Atualizar Simulação

```
PATCH /api/v1/simulacoes/cenarios/{id}/simulacoes/{simId}
Autorização: Escrita
```

Atualiza todos os campos mutáveis de uma simulação. A atualização é **total** (não parcial): todos os campos de `AtualizarSimulacaoInput` devem ser enviados. O campo `version` é incrementado automaticamente pelo domínio para invalidar o cache Redis (AD-3).

Bloqueado se o cenário estiver `Arquivado`.

**Request Body:** `AtualizarSimulacaoInput`

Os campos são os mesmos que `AdicionarSimulacaoInput`, exceto `bancoId` (o banco não pode ser alterado em uma simulação existente — para trocar o banco, remova e adicione novamente).

```json
{
  "modalidade": "Finimp",
  "moeda": "USD",
  "valorPrincipal": 6000000.00,
  "dataContratacaoPrevista": "2026-08-01",
  "dataPrimeiroVencimento": "2027-08-01",
  "tipoTaxa": "Fixa",
  "taxaAa": 6.0,
  "spreadAa": null,
  "baseCalculo": "Dias360",
  "estruturaAmortizacao": "Bullet",
  "periodicidade": "Bullet",
  "quantidadeParcelas": 1,
  "anchorDiaMes": "DiaContratacao",
  "anchorDiaFixo": null,
  "garantiaExigidaPrevista": null,
  "observacoes": "Revisado após negociação"
}
```

**Response 200 OK:** `CenarioSimulacaoDto`

**Erros:**
- `400 Bad Request` — Invariante de domínio violada
- `404 Not Found` — Cenário ou simulação não encontrada
- `409 Conflict` — Cenário está `Arquivado`

---

### Remover Simulação

```
DELETE /api/v1/simulacoes/cenarios/{id}/simulacoes/{simId}
Autorização: Escrita
```

Remove permanentemente a simulação do cenário (hard delete da entidade filha; o cenário permanece). Bloqueado se o cenário estiver `Arquivado`.

**Response 204 No Content**

**Erros:**
- `404 Not Found` — Cenário ou simulação não encontrada
- `409 Conflict` — Cenário está `Arquivado`

---

## Endpoint — Atalho Quadro da Dívida

### Quadro da Dívida por Cenário

```
GET /api/v1/simulacoes/cenarios/{id}/quadro-divida[?ano=YYYY]
Autorização: Leitura
```

Atalho de conveniência equivalente a `GET /api/v1/painel/quadro-divida?cenarioId={id}&ano={ano}`. Busca o cenário para obter o `AnoBase` e o usa como ano padrão quando `?ano` não é informado.

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | Id do cenário de simulação |

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `ano` | int | Não | Sobrepõe o `AnoBase` do cenário. Útil para consultas em ano diferente do base |

**Response 200 OK:** `QuadroDividaDto` (ver [Painel API — Quadro da Dívida](./painel.md#quadro-da-dívida))

**Erros:**
- `400 Bad Request` — Ano fora do intervalo válido
- `404 Not Found` — Cenário não encontrado
- `409 Conflict` — Restrição MVP Q9: ano informado diferente do ano corrente do servidor

---

## Schemas

### CenarioSimulacaoDto

DTO completo do cenário, incluindo todas as simulações filhas. Retornado por Criar, Obter, Atualizar, Ativar, Arquivar e Duplicar.

```json
{
  "id": "guid",
  "nome": "string (máx. 100 chars)",
  "descricao": "string | null",
  "anoBase": "int (2020–2050)",
  "status": "Rascunho | Ativo | Arquivado",
  "criadoPor": "string (actorSub do JWT)",
  "createdAt": "DateTimeOffset (ISO 8601, UTC)",
  "updatedAt": "DateTimeOffset (ISO 8601, UTC)",
  "simulacoes": "SimulacaoContratacaoDto[]"
}
```

---

### CenarioSimulacaoResumoDto

DTO resumido para uso em listagens. Omite as simulações filhas para reduzir o payload.

```json
{
  "id": "guid",
  "nome": "string",
  "status": "Rascunho | Ativo | Arquivado",
  "anoBase": "int",
  "qtdeSimulacoes": "int",
  "criadoPor": "string",
  "updatedAt": "DateTimeOffset"
}
```

---

### SimulacaoContratacaoDto

DTO de uma captação hipotética dentro de um cenário. O campo `version` é incrementado a cada mutação e usado como chave de invalidação do cache Redis (AD-3).

```json
{
  "id": "guid",
  "cenarioId": "guid",
  "bancoId": "guid",
  "modalidade": "Finimp | Refinimp | Lei4131 | Nce | BalcaoCaixa | Fgi",
  "moeda": "BRL | USD | EUR | JPY | CNY",
  "valorPrincipal": "decimal",
  "dataContratacaoPrevista": "YYYY-MM-DD",
  "dataPrimeiroVencimento": "YYYY-MM-DD",
  "tipoTaxa": "Fixa | CdiSpread",
  "taxaAa": "decimal | null",
  "spreadAa": "decimal | null",
  "baseCalculo": "Dias252 | Dias360 | Dias365",
  "estruturaAmortizacao": "Bullet | Price | Sac | Customizada",
  "periodicidade": "Bullet | Mensal | Bimestral | Trimestral | Semestral | Anual",
  "quantidadeParcelas": "int",
  "anchorDiaMes": "DiaContratacao | DiaFixo | UltimoDiaMes",
  "anchorDiaFixo": "int (1–31) | null",
  "garantiaExigidaPrevista": "string (máx. 500 chars) | null",
  "observacoes": "string | null",
  "version": "int",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

| Campo | Descrição |
|-------|-----------|
| `taxaAa` | Taxa fixa anual em %. Presente quando `tipoTaxa = Fixa`; `null` caso contrário |
| `spreadAa` | Spread sobre CDI anual em %. Presente quando `tipoTaxa = CdiSpread`; `null` caso contrário |
| `garantiaExigidaPrevista` | Descrição textual informativa da garantia esperada pelo banco (D-9). Não valida contra limites cadastrados |
| `version` | Contador de versão incrementado a cada mutação. Usado como parte da chave de cache Redis |

---

### AdicionarSimulacaoInput

Input para adicionar uma nova simulação a um cenário. Todos os campos obrigatórios devem ser informados.

```json
{
  "bancoId": "guid",
  "modalidade": "string",
  "moeda": "string",
  "valorPrincipal": "decimal",
  "dataContratacaoPrevista": "YYYY-MM-DD",
  "dataPrimeiroVencimento": "YYYY-MM-DD",
  "tipoTaxa": "Fixa | CdiSpread",
  "taxaAa": "decimal | null",
  "spreadAa": "decimal | null",
  "baseCalculo": "string",
  "estruturaAmortizacao": "string",
  "periodicidade": "string",
  "quantidadeParcelas": "int",
  "anchorDiaMes": "string",
  "anchorDiaFixo": "int | null",
  "garantiaExigidaPrevista": "string | null",
  "observacoes": "string | null"
}
```

---

### AtualizarSimulacaoInput

Input para atualizar os campos mutáveis de uma simulação existente. A atualização é total (não parcial): todos os campos devem ser enviados. `bancoId` não é incluído — para alterar o banco, remova e adicione nova simulação.

Mesmos campos que `AdicionarSimulacaoInput`, exceto `bancoId`.

---

### CronogramaHipoteticoDto

Resultado do preview de cronograma (endpoint `POST /cronograma-hipotetico`).

```json
{
  "taxaEfetivaAaPercentual": "decimal",
  "quantidadeEventos": "int",
  "principalTotal": "decimal",
  "jurosTotal": "decimal",
  "eventos": "EventoCronogramaItemDto[]"
}
```

---

### EventoCronogramaItemDto

Item individual do cronograma hipotético.

```json
{
  "numero": "int",
  "tipo": "string (ex.: 'Principal', 'Juros')",
  "data": "YYYY-MM-DD",
  "valor": "decimal",
  "saldoDevedorApos": "decimal | null"
}
```

---

## Enums

### StatusCenarioSimulacao

| Valor | Descrição |
|-------|-----------|
| `Rascunho` | Cenário em edição; mutável |
| `Ativo` | Cenário aprovado para uso em projeções; ainda mutável |
| `Arquivado` | Cenário encerrado; imutável via API |

### TipoTaxa

| Valor | Descrição |
|-------|-----------|
| `Fixa` | Taxa fixa anual. Requer `taxaAa` preenchido |
| `CdiSpread` | CDI mais spread. Requer `spreadAa` e `cdiReferenciaAaPercentual` preenchidos |

---

## Notas Operacionais

### Idempotency-Key

Os endpoints `POST /cenarios`, `POST /cenarios/{id}/duplicar` e `POST /cenarios/{id}/simulacoes` suportam o header `Idempotency-Key`. Quando um cliente reenvia a mesma requisição com a mesma chave dentro de 24 horas, o servidor retorna a resposta original sem criar duplicatas. Utilize UUIDs como valor da chave.

```
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

### Cache Redis de Cronograma (AD-3)

Os cronogramas das simulações são calculados on-the-fly pelo motor de cronograma existente (`CronogramaStrategyFactory`) e armazenados no Redis com TTL de 60 segundos. A chave de cache é composta por:

```
sim:cronograma:{cenarioId}:{simulacaoId}:v{version}
```

O campo `version` de `SimulacaoContratacaoDto` é incrementado a cada mutação (`AtualizarSimulacao`, `RemoverSimulacao`). Isso torna a chave anterior obsoleta e força o recálculo na próxima consulta, sem necessidade de invalidação explícita.

### Ultrapassar LimiteBanco

Quando uma simulação implica captação que ultrapassa o `valorDisponivelBrl` do `LimiteBanco` vigente para o par banco/modalidade, o sistema gera um alerta não-bloqueante no array `alertas[]` do `QuadroDividaDto`. A operação é salva normalmente — a finalidade da simulação é exatamente avaliar cenários hipotéticos, inclusive além dos limites atuais (D-5 / AD-12).

### Atalho /cenarios/{id}/quadro-divida

O endpoint `GET /api/v1/simulacoes/cenarios/{id}/quadro-divida` é funcionalmente equivalente a:

```
GET /api/v1/painel/quadro-divida?cenarioId={id}&ano={cenario.AnoBase}
```

O parâmetro `?ano=YYYY` sobrepõe o `AnoBase` do cenário quando informado. A restrição MVP Q9 (apenas ano corrente) aplica-se igualmente neste atalho.

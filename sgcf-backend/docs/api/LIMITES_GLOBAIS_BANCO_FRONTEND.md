# Limites Globais de Banco API

**Base route:** `/api/v1/limites-globais-banco`
**Introduzido em:** S33
**Data:** 2026-05-23

---

## 1. Overview

Um `LimiteGlobalBanco` representa o **teto agregado** (linha guarda-chuva) que um banco concede à empresa, independente de modalidade de captação. Ele coexiste com o `LimiteBanco` existente (que continua representando limites por modalidade como Finimp, Lei4131, NCE etc.) e adiciona um teto consolidado acima de todos eles.

O sistema opera em dois regimes por banco, detectados automaticamente:

- **GlobalPuro** — o banco não possui nenhum `LimiteBanco` por modalidade registrado. O limite global é o único teto: qualquer modalidade pode operar sob ele livremente. O campo `valorUtilizadoBrl` no endpoint vigente reflete a soma dos saldos devedores de todos os contratos ativos do banco.

- **PerModalidade** — o banco possui ao menos um `LimiteBanco` (por modalidade) registrado. O limite global atua como **invariante de soma**: a soma de todos os `LimiteBanco` ativos do banco não pode exceder o limite global. O campo `valorUtilizadoBrl` no endpoint vigente reflete a soma de `valorUtilizadoBrl` de cada `LimiteBanco` das modalidades.

O frontend deve exibir o regime em destaque na tela de detalhes do banco. Em regime **GlobalPuro**, a disponibilidade é calculada diretamente a partir do limite global. Em regime **PerModalidade**, existem dois tetos simultâneos para cada operação — o limite da modalidade e o headroom global — e o menor deles prevalece.

---

## 2. Endpoints

### 2.1. Criar Limite Global

```
POST /api/v1/limites-globais-banco
Autorização: Admin
```

Cria um novo limite global para o banco. Apenas um limite com `dataVigenciaFim = null` (open-ended) pode existir por banco por vez — essa restrição é imposta por índice único no banco de dados. Criar um segundo limite com datas sobrepostas retorna `409`.

**Request Body:**

| Campo | Tipo | Obrigatório | Restrições |
|-------|------|-------------|------------|
| `bancoId` | `string (uuid)` | Sim | Banco deve existir |
| `valorLimiteBrl` | `number` | Sim | > 0 |
| `dataVigenciaInicio` | `string (YYYY-MM-DD)` | Sim | — |
| `dataVigenciaFim` | `string (YYYY-MM-DD) \| null` | Não | > `dataVigenciaInicio` quando informado |
| `observacoes` | `string \| null` | Não | Texto livre |

**Exemplo de request:**

```json
{
  "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "valorLimiteBrl": 15000000.00,
  "dataVigenciaInicio": "2026-06-01",
  "dataVigenciaFim": null,
  "observacoes": "Linha aprovada em comitê BB de 21/05/2026"
}
```

**Response `201 Created`:** [`LimiteGlobalBancoDto`](#31-limiteGlobalbancoDTo)

```json
{
  "id": "018f2a3b-0000-7000-8000-000000000001",
  "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "valorLimiteBrl": 15000000.00,
  "dataVigenciaInicio": "2026-06-01",
  "dataVigenciaFim": null,
  "observacoes": "Linha aprovada em comitê BB de 21/05/2026",
  "createdAt": "2026-05-23T14:00:00+00:00",
  "updatedAt": "2026-05-23T14:00:00+00:00",
  "historico": [
    {
      "id": "018f2a3b-0000-7000-8000-000000000002",
      "limiteGlobalBancoId": "018f2a3b-0000-7000-8000-000000000001",
      "valorAnteriorBrl": null,
      "valorNovoBrl": 15000000.00,
      "registradoEm": "2026-05-23T14:00:00+00:00",
      "observacoes": "Criação do limite global"
    }
  ]
}
```

**Erros:**

| Código | Quando ocorre |
|--------|---------------|
| `400 Bad Request` | `valorLimiteBrl` ≤ 0, data inválida ou `bancoId` vazio |
| `404 Not Found` | `bancoId` não encontrado |
| `409 Conflict` | Sobreposição de vigência para o banco (LG-05), ou soma dos `LimiteBanco` existentes excede o valor proposto (LG-13) |

---

### 2.2. Listar Limites Globais

```
GET /api/v1/limites-globais-banco
Autorização: Leitura
```

Retorna todos os limites globais do tenant. Aceita filtros opcionais. Nunca retorna `404` — retorna lista vazia quando nenhum registro atende aos filtros.

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `bancoId` | `uuid` | Filtra por banco |
| `vigentesEm` | `YYYY-MM-DD` | Filtra registros vigentes na data informada (`dataVigenciaInicio <= data <= dataVigenciaFim` ou sem `dataVigenciaFim`) |

**Response `200 OK`:** `LimiteGlobalBancoDto[]`

```json
[
  {
    "id": "018f2a3b-0000-7000-8000-000000000001",
    "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "valorLimiteBrl": 15000000.00,
    "dataVigenciaInicio": "2026-06-01",
    "dataVigenciaFim": null,
    "observacoes": "Linha aprovada em comitê BB de 21/05/2026",
    "createdAt": "2026-05-23T14:00:00+00:00",
    "updatedAt": "2026-05-23T14:00:00+00:00",
    "historico": [...]
  }
]
```

> O campo `historico` é incluído em todos os itens da listagem.

---

### 2.3. Buscar Limite Global por ID

```
GET /api/v1/limites-globais-banco/{id}
Autorização: Leitura
```

Retorna um limite global completo pelo seu identificador, incluindo histórico de alterações de valor.

**Responses:**

| Código | Descrição |
|--------|-----------|
| `200 OK` | [`LimiteGlobalBancoDto`](#31-limiteglobalbancoDTo) com `historico` completo |
| `404 Not Found` | ID não encontrado |

**Exemplo de response `200 OK`:**

```json
{
  "id": "018f2a3b-0000-7000-8000-000000000001",
  "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "valorLimiteBrl": 18000000.00,
  "dataVigenciaInicio": "2026-06-01",
  "dataVigenciaFim": null,
  "observacoes": "Renegociação junho/2026",
  "createdAt": "2026-05-23T14:00:00+00:00",
  "updatedAt": "2026-06-10T09:30:00+00:00",
  "historico": [
    {
      "id": "018f2a3b-0000-7000-8000-000000000003",
      "limiteGlobalBancoId": "018f2a3b-0000-7000-8000-000000000001",
      "valorAnteriorBrl": 15000000.00,
      "valorNovoBrl": 18000000.00,
      "registradoEm": "2026-06-10T09:30:00+00:00",
      "observacoes": "Renegociação junho/2026"
    },
    {
      "id": "018f2a3b-0000-7000-8000-000000000002",
      "limiteGlobalBancoId": "018f2a3b-0000-7000-8000-000000000001",
      "valorAnteriorBrl": null,
      "valorNovoBrl": 15000000.00,
      "registradoEm": "2026-05-23T14:00:00+00:00",
      "observacoes": "Criação do limite global"
    }
  ]
}
```

> O histórico é ordenado por `registradoEm` **decrescente** (entrada mais recente primeiro).

---

### 2.4. Atualizar Limite Global

```
PATCH /api/v1/limites-globais-banco/{id}
Autorização: Admin
```

Atualiza valor e/ou datas de vigência e/ou observações com semântica PATCH: campos `null` ou ausentes preservam o valor atual.

**Request Body:**

| Campo | Tipo | Obrigatório | Comportamento quando omitido |
|-------|------|-------------|------------------------------|
| `valorLimiteBrl` | `number \| null` | Não | Preserva valor atual |
| `dataVigenciaInicio` | `string (YYYY-MM-DD) \| null` | Não | Preserva data atual |
| `dataVigenciaFim` | `string (YYYY-MM-DD) \| null` | Não | Preserva data atual (incluindo `null` = aberto) |
| `observacoes` | `string \| null` | Não | Preserva texto atual |

> **Atenção:** para preservar `dataVigenciaFim` no estado atual, omita o campo. Para explicitamente limpar a data de fim (tornar o limite sem data de encerramento), envie `"dataVigenciaFim": null`.

**Exemplo — aumentar o valor do limite:**

```json
{
  "valorLimiteBrl": 20000000.00,
  "observacoes": "Ampliação negociada em comitê jun/2026"
}
```

**Exemplo — atualizar apenas as observações:**

```json
{
  "observacoes": "Linha renegociada — aditivo assinado em 15/06/2026"
}
```

**Response `200 OK`:** [`LimiteGlobalBancoDto`](#31-limiteglobalbancoDTo) com os valores atualizados.

**Erros:**

| Código | Quando ocorre |
|--------|---------------|
| `400 Bad Request` | `valorLimiteBrl` ≤ 0, datas incoerentes |
| `404 Not Found` | ID não encontrado |
| `409 Conflict` | Redução do valor abaixo do saldo devedor atual (LG-06), ou redução abaixo da soma de `LimiteBanco` das modalidades (LG-10) |

> Quando `valorLimiteBrl` é informado e difere do valor atual, o sistema registra automaticamente uma nova entrada no `historico`.

---

### 2.5. Encerrar Vigência

```
DELETE /api/v1/limites-globais-banco/{id}/vigencia
Autorização: Admin
```

Define a data de fim da vigência de um limite global. Após esta chamada, o limite **imediatamente** deixa de ser considerado vigente — o endpoint `/vigente` passa a retornar `404` independentemente de `dataFim` ser passado ou futuro, pois "vigente" é definido como `dataVigenciaFim == null`. A operação é irreversível — uma vez encerrada a vigência, o campo `dataVigenciaFim` não pode ser removido.

**Request Body:**

| Campo | Tipo | Obrigatório | Restrições |
|-------|------|-------------|------------|
| `dataFim` | `string (YYYY-MM-DD)` | Sim | >= `dataVigenciaInicio` do registro |

**Exemplo de request:**

```json
{
  "dataFim": "2026-12-31"
}
```

**Response `204 No Content`** — sem corpo.

**Erros:**

| Código | Quando ocorre |
|--------|---------------|
| `400 Bad Request` | `dataFim` é uma data inválida ou não informada |
| `409 Conflict` | Vigência já encerrada (LG-08), ou `dataFim` anterior a `dataVigenciaInicio` (LG-08) |

---

### 2.6. Obter Limite Global Vigente do Banco

```
GET /api/v1/bancos/{bancoId}/limite-global-vigente
Autorização: Leitura
```

Retorna o limite global **vigente** do banco — definido estritamente como aquele com `dataVigenciaFim == null` (sem data de encerramento). Limites com `dataVigenciaFim` preenchida, mesmo que a data seja futura, **não são retornados** por este endpoint. Inclui os valores computados em tempo de consulta: `valorUtilizadoBrl`, `valorDisponivelBrl` e o `regime` operacional do banco.

> **Atenção para o frontend:** ao chamar `EncerrarVigencia` em um limite (mesmo com `dataFim` no futuro), esse endpoint passa imediatamente a retornar `404`. Trate o `404` como estado definitivo "sem limite ativo", não como erro transitório.

Este é o endpoint principal para o frontend exibir o painel de exposição de um banco.

**Parâmetros de rota:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `bancoId` | `uuid` | ID do banco |

**Response `200 OK`:** [`LimiteGlobalBancoVigenteDto`](#33-limiteglobalbancoVigenteDto)

```json
{
  "id": "018f2a3b-0000-7000-8000-000000000001",
  "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "valorLimiteBrl": 15000000.00,
  "valorUtilizadoBrl": 7200000.00,
  "valorDisponivelBrl": 7800000.00,
  "regime": "PerModalidade",
  "dataVigenciaInicio": "2026-06-01",
  "dataVigenciaFim": null,
  "observacoes": "Linha aprovada em comitê BB de 21/05/2026",
  "createdAt": "2026-05-23T14:00:00+00:00",
  "updatedAt": "2026-05-23T14:00:00+00:00",
  "historico": [
    {
      "id": "018f2a3b-0000-7000-8000-000000000002",
      "limiteGlobalBancoId": "018f2a3b-0000-7000-8000-000000000001",
      "valorAnteriorBrl": null,
      "valorNovoBrl": 15000000.00,
      "registradoEm": "2026-05-23T14:00:00+00:00",
      "observacoes": "Criação do limite global"
    }
  ]
}
```

**Exemplo — banco em regime GlobalPuro:**

```json
{
  "id": "018f2a3b-0000-7000-8000-000000000010",
  "bancoId": "aaaaaaaa-0000-0000-0000-000000000001",
  "valorLimiteBrl": 5000000.00,
  "valorUtilizadoBrl": 1200000.00,
  "valorDisponivelBrl": 3800000.00,
  "regime": "GlobalPuro",
  "dataVigenciaInicio": "2026-01-01",
  "dataVigenciaFim": null,
  "observacoes": null,
  "createdAt": "2026-01-10T10:00:00+00:00",
  "updatedAt": "2026-01-10T10:00:00+00:00",
  "historico": [...]
}
```

**Erros:**

| Código | Quando ocorre |
|--------|---------------|
| `404 Not Found` | Nenhum limite global vigente cadastrado para o banco |

---

## 3. Response Shapes

### 3.1. LimiteGlobalBancoDto

Retornado pelos endpoints de criação (`201`), listagem (`200`), detalhe por ID (`200`) e atualização (`200`).

```typescript
interface LimiteGlobalBancoDto {
  id: string;                          // UUID v7
  bancoId: string;                     // UUID do banco
  valorLimiteBrl: number;              // Valor do teto em BRL (ex.: 15000000.00)
  dataVigenciaInicio: string;          // YYYY-MM-DD — inclusivo
  dataVigenciaFim: string | null;      // YYYY-MM-DD — inclusivo; null = sem data de encerramento
  observacoes: string | null;
  createdAt: string;                   // ISO 8601 com offset (ex.: "2026-05-23T14:00:00+00:00")
  updatedAt: string;                   // ISO 8601 com offset
  historico: LimiteGlobalBancoHistoricoDto[]; // Ordenado por registradoEm decrescente
}
```

### 3.2. LimiteGlobalBancoHistoricoDto

Contido no array `historico` de `LimiteGlobalBancoDto` e `LimiteGlobalBancoVigenteDto`.

```typescript
interface LimiteGlobalBancoHistoricoDto {
  id: string;                          // UUID v7
  limiteGlobalBancoId: string;         // UUID do limite global pai
  valorAnteriorBrl: number | null;     // null apenas na entrada de criação do limite
  valorNovoBrl: number;                // Valor após a alteração
  registradoEm: string;                // ISO 8601 com offset
  observacoes: string | null;
}
```

> `valorAnteriorBrl` é `null` exclusivamente na primeira entrada do histórico (criação do limite). Todas as entradas subsequentes têm o valor anterior preenchido.

### 3.3. LimiteGlobalBancoVigenteDto

Retornado apenas pelo endpoint `GET /api/v1/bancos/{bancoId}/limite-global-vigente`. Estende `LimiteGlobalBancoDto` com três campos computados em tempo de consulta.

```typescript
interface LimiteGlobalBancoVigenteDto {
  id: string;
  bancoId: string;
  valorLimiteBrl: number;
  dataVigenciaInicio: string;          // YYYY-MM-DD — inclusivo
  dataVigenciaFim: string | null;      // YYYY-MM-DD — inclusivo; null = sem data de encerramento
  observacoes: string | null;
  createdAt: string;
  updatedAt: string;
  historico: LimiteGlobalBancoHistoricoDto[];
  // Campos computados — não persistidos:
  valorUtilizadoBrl: number;           // Ver semântica por regime na seção 7
  valorDisponivelBrl: number;          // max(0, valorLimiteBrl - valorUtilizadoBrl)
  regime: 'GlobalPuro' | 'PerModalidade';
}
```

---

## 4. Regras de Negócio Visíveis ao Frontend

As regras a seguir afetam diretamente a experiência do usuário — o backend retorna `409 Conflict` em todos os casos, com uma mensagem de erro em português no campo `error` do corpo da resposta.

| Regra | Endpoint afetado | Quando o 409 ocorre |
|-------|-----------------|---------------------|
| **LG-05** | `POST /api/v1/limites-globais-banco` | O banco já possui um limite global cuja vigência se sobrepõe ao período informado. Só pode existir um limite vigente por banco em cada data. |
| **LG-06** | `PATCH /api/v1/limites-globais-banco/{id}` | O novo valor seria menor que o saldo devedor corrente do banco. A redução só é permitida se o novo limite for igual ou superior ao total já utilizado. |
| **LG-08** | `DELETE /api/v1/limites-globais-banco/{id}/vigencia` | `dataFim` é anterior a `dataVigenciaInicio` do registro, ou a vigência já foi encerrada anteriormente. |
| **LG-09** | `POST /api/v1/limites-banco` e `PATCH /api/v1/limites-banco/{id}` | A criação ou atualização de um `LimiteBanco` (por modalidade) faria a soma dos limites de modalidades do banco exceder o limite global vigente. |
| **LG-10** | `PATCH /api/v1/limites-globais-banco/{id}` | Em regime PerModalidade: o novo valor global seria menor que a soma dos `LimiteBanco` das modalidades ativos. |
| **LG-13** | `POST /api/v1/limites-globais-banco` | O banco já possui limites por modalidade cuja soma excede o valor proposto para o novo limite global. |

**Formato do corpo de erro `409`:**

```json
{
  "error": "Soma dos limites por modalidade (BRL 12000000.00) excede o novo limite global proposto (BRL 10000000.00)."
}
```

---

## 5. Breaking Changes e Endpoints Modificados

### Novo endpoint em `/api/v1/bancos`

O endpoint `GET /api/v1/bancos/{bancoId}/limite-global-vigente` foi adicionado ao `BancosController`. Nenhum campo existente do `BancoDto` foi alterado.

### LimiteBanco — novo erro 409 possível

Os endpoints `POST /api/v1/limites-banco` e `PATCH /api/v1/limites-banco/{id}` passaram a aplicar a regra **LG-09**: se o banco possuir um limite global vigente, a soma de todos os limites por modalidade não pode exceder o valor global. Chamadas que antes retornavam `200` ou `201` podem agora retornar `409 Conflict` se o banco tiver um limite global cadastrado e a operação violaria esse teto.

**Ação necessária no frontend:** verificar e tratar o novo `409` nos formulários de criação e edição de `LimiteBanco`. A mensagem no campo `error` do corpo identifica a causa.

---

## 6. Vigência (Semântica de Datas)

Todos os campos de data são strings no formato `YYYY-MM-DD`, sem componente de horário.

| Campo | Semântica |
|-------|-----------|
| `dataVigenciaInicio` | Inclusivo — o limite está em vigor a partir desta data, inclusive |
| `dataVigenciaFim` | Inclusivo — o limite está em vigor até esta data, inclusive; `null` significa sem data de encerramento (vigente indefinidamente) |

**Definição de "vigente":** um limite é vigente quando `dataVigenciaInicio <= hoje` e (`dataVigenciaFim` é `null` ou `dataVigenciaFim >= hoje`).

O backend garante que não podem existir dois limites globais com períodos sobrepostos para o mesmo banco. Quando o frontend exibe uma linha do tempo de limites de um banco, pode assumir que os períodos são mutuamente exclusivos.

**Dica de exibição:** para um limite com `dataVigenciaFim: null`, exiba "sem data de encerramento" ou "vigente em aberto" — não exiba um campo vazio ou "0001-01-01".

---

## 7. Campo `regime` no Endpoint Vigente

O campo `regime` do `LimiteGlobalBancoVigenteDto` tem dois valores possíveis:

### `"GlobalPuro"`

- O banco **não possui** nenhum `LimiteBanco` por modalidade registrado.
- `valorUtilizadoBrl` = soma dos saldos devedores de todos os contratos ativos do banco, independente de modalidade.
- `valorDisponivelBrl` = `valorLimiteBrl - valorUtilizadoBrl` (piso em zero).
- **Sugestão de UX:** exibir um único indicador de exposição global. Não exibir tabela de modalidades (não há nenhuma registrada).

### `"PerModalidade"`

- O banco possui ao menos um `LimiteBanco` ativo.
- `valorUtilizadoBrl` = soma de `valorUtilizadoBrl` de cada `LimiteBanco` das modalidades vigentes.
- `valorDisponivelBrl` = `valorLimiteBrl - valorUtilizadoBrl` (piso em zero).
- **Sugestão de UX:** exibir o painel de exposição global como um totalizador acima dos limites por modalidade. O headroom global disponível é um segundo limite que coexiste com a disponibilidade individual de cada modalidade — para uma nova operação, o disponível efetivo é `min(disponivel_modalidade, valorDisponivelBrl_global)`.

---

## 8. Notas de Implementação para o Frontend

- `valorDisponivelBrl` é calculado pelo backend a cada chamada ao endpoint vigente — nunca é persistido. Para exibir disponibilidade em tempo real, recarregue o endpoint após cada criação ou conversão de contrato.
- O histórico em `LimiteGlobalBancoDto` e `LimiteGlobalBancoVigenteDto` é **sempre incluído** (não há endpoint separado para histórico). O frontend pode exibir a linha do tempo diretamente sem chamada adicional.
- Todos os valores monetários são `number` em BRL, com até 6 casas decimais internas, mas normalmente 2 casas para exibição.
- Os campos `createdAt`, `updatedAt` e `registradoEm` são strings ISO 8601 com offset UTC (`+00:00`). Converta para o fuso do usuário no frontend conforme necessário.

---

## Referências

- [SPEC_LIMITE_GLOBAL.md](../specs/limites-banco/SPEC_LIMITE_GLOBAL.md)
- [Limites por Modalidade API](./limites-banco.md)
- [Breaking Change S32 — Antecipação por Modalidade](./BREAKING_S32_ANTECIPACAO_POR_MODALIDADE.md)
- [Bancos API](./bancos.md)
- [Schemas compartilhados](./schemas.md)

# Referência da API — Simulações + Quadro da Dívida

**Base URL:** `/api/v1`  
**Autenticação:** JWT Bearer em todos os endpoints.  
**Content-Type:** `application/json` para corpo de requisição e resposta.

---

## 1. Cronograma hipotético (preview stateless)

### `POST /api/v1/simulacoes/cronograma-hipotetico`

Calcula o cronograma de amortização e juros de uma captação hipotética sem persistir nenhum dado. Útil para o front-end pré-visualizar o fluxo financeiro antes de o usuário salvar a simulação em um cenário.

O endpoint é **stateless**: constrói uma `SimulacaoContratacao` temporária, delega ao motor de cronograma e descarta. Pode ser chamado sem nenhum cenário criado.

**Policy:** `Escrita`  
**Idempotency-Key:** Não aplicável (endpoint de leitura computacional).

#### Request body

```json
{
  "simulacao": {
    "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "modalidade": "CapitalDeGiro",
    "moeda": "Brl",
    "valorPrincipal": 5000000.00,
    "dataContratacaoPrevista": "2026-07-01",
    "dataPrimeiroVencimento": "2026-08-01",
    "tipoTaxa": "CdiSpread",
    "taxaAa": null,
    "spreadAa": 2.50,
    "baseCalculo": "Dias252",
    "estruturaAmortizacao": "Price",
    "periodicidade": "Mensal",
    "quantidadeParcelas": 24,
    "anchorDiaMes": "DiaContratacao",
    "anchorDiaFixo": null,
    "garantiaExigidaPrevista": "Alienação fiduciária de recebíveis",
    "observacoes": "Operação de capital de giro para expansão"
  },
  "cdiReferenciaAaPercentual": 10.50
}
```

| Campo | Tipo | Obrigatório | Notas |
|---|---|---|---|
| `simulacao` | `AdicionarSimulacaoInput` | Sim | Ver [dtos.md](./dtos.md) |
| `cdiReferenciaAaPercentual` | `decimal?` | Condicional | Obrigatório quando `tipoTaxa = "CdiSpread"` |

#### Response — 200 OK

```json
{
  "taxaEfetivaAaPercentual": 13.26,
  "quantidadeEventos": 24,
  "principalTotal": 5000000.00,
  "jurosTotal": 683421.75,
  "eventos": [
    {
      "numero": 1,
      "tipo": "Principal",
      "data": "2026-08-01",
      "valor": 208333.33,
      "saldoDevedorApos": 4791666.67
    },
    {
      "numero": 2,
      "tipo": "Juros",
      "data": "2026-08-01",
      "valor": 42875.00,
      "saldoDevedorApos": null
    }
  ]
}
```

| Campo | Tipo | Descrição |
|---|---|---|
| `taxaEfetivaAaPercentual` | `decimal` | Taxa efetiva anual em %. Para CdiSpread: `(1+CDI)×(1+spread)−1 × 100`. |
| `quantidadeEventos` | `int` | Total de linhas no cronograma (principal + juros). |
| `principalTotal` | `decimal` | Soma de todos os eventos do tipo `"Principal"`. |
| `jurosTotal` | `decimal` | Soma de todos os eventos do tipo `"Juros"`. |
| `eventos[].numero` | `int` | Número sequencial do evento. |
| `eventos[].tipo` | `string` | `"Principal"` ou `"Juros"`. |
| `eventos[].data` | `string` | Data do evento em `"YYYY-MM-DD"`. |
| `eventos[].valor` | `decimal` | Valor em BRL (ou na moeda da operação). |
| `eventos[].saldoDevedorApos` | `decimal?` | Saldo devedor após o evento. `null` para eventos de Juros. |

#### Status codes

| Código | Causa |
|---|---|
| 200 | Cronograma calculado com sucesso. |
| 400 | Invariante de domínio violada (ex: `valorPrincipal = 0`, `dataPrimeiroVencimento <= dataContratacaoPrevista`, `tipoTaxa = CdiSpread` sem `cdiReferenciaAaPercentual`). |
| 401 | Token ausente ou expirado. |
| 403 | Usuário sem policy `Escrita`. |
| 409 | Estado interno inválido (improvável em uso normal). |

---

## 2. Cenários de simulação

### `POST /api/v1/simulacoes/cenarios`

Cria um novo cenário em status `Rascunho`. O campo `criadoPor` é preenchido automaticamente com o `sub` do JWT.

**Policy:** `Escrita`  
**Idempotency-Key:** Recomendado. TTL de 24 horas.

#### Request body

```json
{
  "nome": "Realista 2026",
  "anoBase": 2026,
  "descricao": "Cenário com captações previstas para expansão da unidade de São Paulo"
}
```

| Campo | Tipo | Obrigatório | Constraints |
|---|---|---|---|
| `nome` | `string` | Sim | Máx. 100 caracteres, não vazio. |
| `anoBase` | `int` | Sim | Entre 2020 e 2050 inclusive. |
| `descricao` | `string?` | Não | Texto livre. |

#### Response — 201 Created

Retorna `CenarioSimulacaoDto`. Ver [dtos.md](./dtos.md) para a definição completa.

```json
{
  "id": "7b3e4f2a-1c5d-4e8b-9a0f-2c6d8e1f3b5a",
  "nome": "Realista 2026",
  "descricao": "Cenário com captações previstas para expansão da unidade de São Paulo",
  "anoBase": 2026,
  "status": "Rascunho",
  "criadoPor": "auth0|user123",
  "createdAt": "2026-05-20T14:30:00-03:00",
  "updatedAt": "2026-05-20T14:30:00-03:00",
  "simulacoes": []
}
```

#### Status codes

| Código | Causa |
|---|---|
| 201 | Cenário criado. Header `Location` aponta para `GET /cenarios/{id}`. |
| 400 | `nome` vazio, `nome` com mais de 100 chars, `anoBase` fora de [2020, 2050]. |
| 401 | Token ausente ou expirado. |
| 403 | Usuário sem policy `Escrita`. |

---

### `GET /api/v1/simulacoes/cenarios`

Lista cenários visíveis (soft-deletados excluídos) com filtros opcionais.

**Policy:** `Leitura`

#### Query parameters

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `status` | `string?` | Filtro por status: `Rascunho`, `Ativo` ou `Arquivado`. |
| `anoBase` | `int?` | Filtro por ano-base. |
| `criadoPor` | `string?` | Filtro pelo `sub` do criador. |

#### Response — 200 OK

Array de `CenarioSimulacaoResumoDto` (sem simulações filhas para reduzir payload).

```json
[
  {
    "id": "7b3e4f2a-1c5d-4e8b-9a0f-2c6d8e1f3b5a",
    "nome": "Realista 2026",
    "status": "Ativo",
    "anoBase": 2026,
    "qtdeSimulacoes": 3,
    "criadoPor": "auth0|user123",
    "updatedAt": "2026-05-20T16:00:00-03:00"
  }
]
```

---

### `GET /api/v1/simulacoes/cenarios/{id}`

Retorna o cenário completo incluindo todas as simulações filhas.

**Policy:** `Leitura`

#### Path parameters

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `id` | `Guid` | Identificador do cenário. |

#### Response — 200 OK

`CenarioSimulacaoDto` com a lista `simulacoes` preenchida. Ver [dtos.md](./dtos.md).

#### Status codes

| Código | Causa |
|---|---|
| 200 | Cenário encontrado. |
| 404 | Cenário não existe ou foi soft-deletado. |

---

### `PATCH /api/v1/simulacoes/cenarios/{id}`

Atualiza nome, descrição e/ou anoBase de um cenário. Campos `null` não são alterados (patch parcial).

**Regra de domínio:** `AnoBase` só pode ser alterado quando o cenário está em `Rascunho`. Em `Ativo`, a tentativa de mudar `AnoBase` retorna 409.  
**Policy:** `Escrita`

#### Request body

```json
{
  "nome": "Realista 2026 — Revisado",
  "descricao": "Nova descrição após revisão do CFO",
  "anoBase": null
}
```

| Campo | Tipo | Obrigatório | Notas |
|---|---|---|---|
| `nome` | `string?` | Não | Quando não null: não vazio, máx. 100 chars. |
| `descricao` | `string?` | Não | Quando não null: substitui a descrição atual. |
| `anoBase` | `int?` | Não | Quando não null: entre 2020 e 2050. Só permite mudança em `Rascunho`. |

#### Response — 200 OK

`CenarioSimulacaoDto` atualizado.

#### Status codes

| Código | Causa |
|---|---|
| 200 | Cenário atualizado. |
| 400 | Campo com valor inválido. |
| 404 | Cenário não encontrado. |
| 409 | Tentativa de mudar `AnoBase` em cenário `Ativo` ou `Arquivado`; ou cenário `Arquivado` sendo editado. |

---

### `POST /api/v1/simulacoes/cenarios/{id}/ativar`

Transita o cenário de `Rascunho` para `Ativo`.

**Policy:** `Escrita`

#### Response — 200 OK

`CenarioSimulacaoDto` com `status: "Ativo"`.

#### Status codes

| Código | Causa |
|---|---|
| 200 | Cenário ativado. |
| 404 | Cenário não encontrado. |
| 409 | Cenário já está `Ativo` ou está `Arquivado`. |

---

### `POST /api/v1/simulacoes/cenarios/{id}/arquivar`

Arquiva um cenário `Ativo`. Operação **irreversível via API**. Apenas usuários com policy `Gerencial` podem executar esta operação (decisão AD-11).

**Policy:** `Gerencial`

#### Response — 200 OK

`CenarioSimulacaoDto` com `status: "Arquivado"`.

#### Status codes

| Código | Causa |
|---|---|
| 200 | Cenário arquivado. |
| 404 | Cenário não encontrado. |
| 409 | Cenário não está `Ativo` (somente cenários Ativos podem ser arquivados). |

---

### `POST /api/v1/simulacoes/cenarios/{id}/duplicar`

Cria uma cópia profunda do cenário em status `Rascunho` com novo `Id`. O nome da cópia recebe o sufixo `" (cópia)"`. Todas as simulações filhas são copiadas com novos Ids e `Version = 1`.

**Invariante:** Cenários com soft delete (`DeletedAt` preenchido) não podem ser duplicados.  
**Policy:** `Escrita`  
**Idempotency-Key:** Recomendado. TTL de 24 horas.

#### Response — 201 Created

`CenarioSimulacaoDto` da cópia.

#### Status codes

| Código | Causa |
|---|---|
| 201 | Cópia criada. Header `Location` aponta para `GET /cenarios/{novoId}`. |
| 404 | Cenário original não encontrado (ou soft-deletado). |

---

### `DELETE /api/v1/simulacoes/cenarios/{id}`

Realiza soft delete do cenário. O registro permanece no banco com `DeletadoEm` preenchido. Chamadas subsequentes de `GET /cenarios/{id}` retornam 404. A listagem exclui automaticamente cenários soft-deletados.

**Policy:** `Escrita`

#### Response — 204 No Content

Sem corpo.

#### Status codes

| Código | Causa |
|---|---|
| 204 | Deletado com sucesso. |
| 404 | Cenário não encontrado. |

---

## 3. Simulações dentro de um cenário

### `POST /api/v1/simulacoes/cenarios/{id}/simulacoes`

Adiciona uma nova simulação de contratação ao cenário. O endpoint retorna o cenário completo atualizado (incluindo a nova simulação). Bloqueado em cenários `Arquivados`.

**Policy:** `Escrita`  
**Idempotency-Key:** Recomendado. TTL de 24 horas.

#### Path parameters

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `id` | `Guid` | Identificador do cenário pai. |

#### Request body

`AdicionarSimulacaoInput`. Ver [dtos.md](./dtos.md) para todos os campos.

```json
{
  "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "modalidade": "CapitalDeGiro",
  "moeda": "Brl",
  "valorPrincipal": 10000000.00,
  "dataContratacaoPrevista": "2026-09-01",
  "dataPrimeiroVencimento": "2026-10-01",
  "tipoTaxa": "Fixa",
  "taxaAa": 15.50,
  "spreadAa": null,
  "baseCalculo": "Dias252",
  "estruturaAmortizacao": "Sac",
  "periodicidade": "Mensal",
  "quantidadeParcelas": 36,
  "anchorDiaMes": "DiaContratacao",
  "anchorDiaFixo": null,
  "garantiaExigidaPrevista": null,
  "observacoes": null
}
```

#### Response — 201 Created

`CenarioSimulacaoDto` com a nova simulação incluída na lista `simulacoes`.

#### Status codes

| Código | Causa |
|---|---|
| 201 | Simulação adicionada. Header `Location` aponta para `GET /cenarios/{id}`. |
| 400 | Invariante de domínio violada (ver invariantes I-1..I-11 em [dtos.md](./dtos.md)). |
| 404 | Cenário não encontrado. |
| 409 | Cenário `Arquivado` — operações de mutação bloqueadas. |

---

### `PATCH /api/v1/simulacoes/cenarios/{id}/simulacoes/{simId}`

Atualiza todos os campos mutáveis de uma simulação existente (substituição total, não parcial). O `Version` da simulação é incrementado automaticamente para invalidar o cache Redis.

**Policy:** `Escrita`

#### Path parameters

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `id` | `Guid` | Identificador do cenário pai. |
| `simId` | `Guid` | Identificador da simulação. |

#### Request body

`AtualizarSimulacaoInput`. Campos idênticos ao `AdicionarSimulacaoInput`, exceto que `bancoId` não é alterável. Ver [dtos.md](./dtos.md).

#### Response — 200 OK

`CenarioSimulacaoDto` completo com a simulação atualizada.

#### Status codes

| Código | Causa |
|---|---|
| 200 | Simulação atualizada. |
| 400 | Invariante de domínio violada. |
| 404 | Cenário ou simulação não encontrada. |
| 409 | Cenário `Arquivado`. |

---

### `DELETE /api/v1/simulacoes/cenarios/{id}/simulacoes/{simId}`

Remove uma simulação do cenário (hard delete da simulação filha; o cenário pai permanece). Bloqueado em cenários `Arquivados`.

**Policy:** `Escrita`

#### Response — 204 No Content

Sem corpo.

#### Status codes

| Código | Causa |
|---|---|
| 204 | Simulação removida. |
| 404 | Cenário ou simulação não encontrada. |
| 409 | Cenário `Arquivado`. |

---

## 4. Atalho: Quadro da Dívida a partir de um cenário

### `GET /api/v1/simulacoes/cenarios/{id}/quadro-divida`

Endpoint de conveniência que retorna o Quadro da Dívida com o cenário já aplicado. Internamente obtém o `AnoBase` do cenário e delega ao `GetQuadroDividaQuery` passando o `cenarioId`. O parâmetro opcional `?ano=` sobrepõe o `AnoBase` do cenário.

**Policy:** `Leitura`

#### Path parameters

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `id` | `Guid` | Identificador do cenário de simulação. |

#### Query parameters

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `ano` | `int?` | Sobrepõe o `AnoBase` do cenário. Útil para consultas históricas. |

#### Response — 200 OK

`QuadroDividaDto`. Ver seção 5 deste documento e [quadro-divida.md](./quadro-divida.md).

#### Status codes

| Código | Causa |
|---|---|
| 200 | Quadro calculado. |
| 400 | Ano informado fora do intervalo válido (2020–2050). |
| 404 | Cenário não encontrado. |
| 409 | Restrição MVP: ano diferente do ano corrente do servidor (Q9). |

---

## 5. Comparativo entre cenários

### `POST /api/v1/simulacoes/comparar`

Compara até 5 cenários retornando a projeção de cada um com deltas mensais e anuais em relação ao primeiro cenário (baseline). O primeiro `cenarioId` da lista é sempre o baseline — seus campos `deltasMensais` e `deltaAnual` são sempre `null`.

**Policy:** `Leitura`

#### Request body

```json
{
  "ano": 2026,
  "cenarioIds": [
    "7b3e4f2a-1c5d-4e8b-9a0f-2c6d8e1f3b5a",
    "9c4d5e3b-2f6e-5f9c-0b1g-3d7e9f2g4c6b",
    "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"
  ]
}
```

| Campo | Tipo | Constraints |
|---|---|---|
| `ano` | `int` | Entre 2020 e 2050. |
| `cenarioIds` | `Guid[]` | Mínimo 1, máximo 5. Todos devem ter o mesmo `AnoBase`. |

#### Response — 200 OK

```json
{
  "ano": 2026,
  "dataReferencia": "2026-05-20",
  "cenarios": [
    {
      "cenarioId": "7b3e4f2a-1c5d-4e8b-9a0f-2c6d8e1f3b5a",
      "nome": "Baseline — sem novas captações",
      "status": "Ativo",
      "anoBase": 2026,
      "ehBaseline": true,
      "projecao": { "meses": [ ... ] },
      "sumario": {
        "saldoTotalInicioAno": 250000000.00,
        "saldoTotalFimAno": 210000000.00,
        "totalAmortizacaoNoAno": 40000000.00,
        "totalCaptacaoNoAno": 0.00,
        "variacaoAnualPercentual": -16.00
      },
      "deltasMensais": null,
      "deltaAnual": null
    },
    {
      "cenarioId": "9c4d5e3b-2f6e-5f9c-0b1g-3d7e9f2g4c6b",
      "nome": "Realista 2026",
      "status": "Ativo",
      "anoBase": 2026,
      "ehBaseline": false,
      "projecao": { "meses": [ ... ] },
      "sumario": { ... },
      "deltasMensais": [
        {
          "mes": 1,
          "saldoFimDelta": 0.00,
          "totalCaptacaoDelta": 0.00,
          "totalAmortizacaoDelta": 0.00,
          "saldoFimDeltaPercentual": 0.00
        },
        {
          "mes": 9,
          "saldoFimDelta": 10000000.00,
          "totalCaptacaoDelta": 10000000.00,
          "totalAmortizacaoDelta": 0.00,
          "saldoFimDeltaPercentual": 4.76
        }
      ],
      "deltaAnual": {
        "saldoFimAnoDelta": 8500000.00,
        "totalCaptacaoAnoDelta": 10000000.00,
        "saldoFimAnoDeltaPercentual": 4.05
      }
    }
  ]
}
```

#### Status codes

| Código | Causa |
|---|---|
| 200 | Comparativo calculado. |
| 400 | Lista vazia ou com mais de 5 cenários. |
| 404 | Algum `cenarioId` não existe. |
| 409 | Cenários com `AnoBase` diferentes na mesma comparação. |

---

## 6. Quadro da Dívida — Painel

### `GET /api/v1/painel/quadro-divida`

Retorna o Quadro da Dívida para o ano informado. Sem `cenarioId`, retorna apenas dados reais (contratos ativos + amortizações futuras). Com `cenarioId`, incorpora as captações hipotéticas do cenário na projeção (AD-9).

**Restrição MVP (Q9):** Apenas o ano corrente do servidor é suportado. Anos diferentes retornam 409.  
**Policy:** `Leitura`

#### Query parameters

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `ano` | `int?` | Ano da projeção. Omitido = ano corrente (fuso `America/Sao_Paulo`). |
| `cenarioId` | `Guid?` | Cenário a aplicar na projeção. |

#### Response — 200 OK

```json
{
  "ano": 2026,
  "dataReferencia": "2026-05-20",
  "snapshotInicial": {
    "bancos": [
      {
        "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "bancoApelido": "Itaú",
        "bancoCodigoCompe": "341",
        "saldoBrl": 80000000.00,
        "quantidadeContratosAtivos": 4
      }
    ],
    "saldoTotalBrl": 250000000.00,
    "dataReferencia": "2026-05-20"
  },
  "projecao": {
    "meses": [
      {
        "ano": 2026,
        "mes": 1,
        "bancos": [
          {
            "bancoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "bancoApelido": "Itaú",
            "saldoInicio": 80000000.00,
            "saldoFim": 76500000.00,
            "totalAmortizacaoNoMes": 3500000.00,
            "totalCaptacaoNoMes": 0.00,
            "sharePercentual": 36.43
          }
        ],
        "saldoTotalInicio": 250000000.00,
        "saldoTotalFim": 246500000.00,
        "totalAmortizacaoMes": 3500000.00,
        "totalCaptacaoMes": 0.00
      }
    ]
  },
  "sumario": {
    "saldoTotalInicioAno": 250000000.00,
    "saldoTotalFimAno": 210000000.00,
    "totalAmortizacaoNoAno": 40000000.00,
    "totalCaptacaoNoAno": 0.00,
    "variacaoAnualPercentual": -16.00
  },
  "alertas": [],
  "cenarioAplicado": null
}
```

Quando `cenarioId` é informado, `cenarioAplicado` contém os metadados do cenário:

```json
"cenarioAplicado": {
  "id": "7b3e4f2a-1c5d-4e8b-9a0f-2c6d8e1f3b5a",
  "nome": "Realista 2026",
  "status": "Ativo",
  "anoBase": 2026,
  "quantidadeSimulacoes": 3
}
```

#### Status codes

| Código | Causa |
|---|---|
| 200 | Quadro calculado. |
| 400 | Ano fora de [2020, 2050]. |
| 404 | `cenarioId` informado não existe. |
| 409 | Ano diferente do ano corrente do servidor (restrição MVP Q9). |

---

## 7. Parâmetros do sistema

### `GET /api/v1/parametros-sistema`

Retorna os parâmetros globais do sistema.

**Policy:** `Leitura`

#### Response — 200 OK

```json
{
  "tetaoMensalCapacidadeBrl": 50000000.00
}
```

`tetaoMensalCapacidadeBrl` é `null` quando nenhum tetão foi configurado (sem limite).

---

### `PATCH /api/v1/parametros-sistema/tetao-mensal`

Configura o tetão mensal de movimentação em BRL. Quando algum mês da projeção tiver `(totalAmortizacaoMes + totalCaptacaoMes) > tetão`, o campo `alertas` do Quadro da Dívida conterá uma mensagem por mês excedido.

**Policy:** `Admin`

#### Request body

```json
{
  "valor": 50000000.00
}
```

Passe `null` em `valor` para remover o limite:

```json
{
  "valor": null
}
```

| Campo | Tipo | Notas |
|---|---|---|
| `valor` | `decimal?` | Valor em BRL. Não pode ser negativo. `null` remove o limite. |

#### Response — 200 OK

`ParametrosSistemaDto` atualizado:

```json
{
  "tetaoMensalCapacidadeBrl": 50000000.00
}
```

#### Status codes

| Código | Causa |
|---|---|
| 200 | Parâmetro atualizado. |
| 400 | Valor negativo. |
| 401 | Token ausente ou expirado. |
| 403 | Usuário sem policy `Admin`. |

# Limites de Banco API

**Base route:** `/api/v1/limites-banco`

Gerencia os limites operacionais que cada banco concede para cada modalidade de captação. O `valorDisponivelBrl` é validado no momento de adicionar bancos-alvo a uma [Cotação](./cotacoes.md) e atualizado quando uma cotação é convertida em contrato (SPEC §3.2 regra 8, §4.1).

---

## Endpoints

### Listar Limites

```
GET /api/v1/limites-banco
Autorização: Leitura
```

**Query Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `bancoId` | guid | Filtra por banco |
| `modalidade` | string | Filtra por [ModalidadeContrato](./schemas.md#modalidadecontrato) |

**Response 200 OK:** [LimiteBancoDto](#limitebancodto)`[]`

---

### Buscar Limite por ID

```
GET /api/v1/limites-banco/{id}
Autorização: Leitura
```

Retorna o limite completo incluindo garantias exigidas e histórico de valores concedidos.

**Responses:**
- `200 OK` — [LimiteBancoDto](#limitebancodto) (com `garantiasExigidas` e `historico` populados)
- `404 Not Found`

---

### Criar Limite

```
POST /api/v1/limites-banco
Autorização: Admin
```

Cria novo limite operacional para banco/modalidade. A criação registra automaticamente a primeira entrada no histórico de valores (`valorAnteriorBrl = null`).

**Request Body:**

```json
{
  "bancoId": "guid",
  "modalidade": "Finimp",
  "valorLimiteBrl": 50000000.00,
  "dataVigenciaInicio": "2026-01-01",
  "dataVigenciaFim": "2026-12-31",
  "observacoes": "Limite aprovado em comitê de crédito 2026",
  "garantiasExigidas": [
    {
      "tipo": "CdbCativo",
      "percentualSobreLimite": 20.0,
      "obrigatoria": true,
      "observacoes": "CDB cativo no próprio banco"
    }
  ]
}
```

**Exemplo — Limite sem garantias formais (modalidade Aval implícito):**

```json
{
  "bancoId": "guid",
  "modalidade": "Finimp",
  "valorLimiteBrl": 30000000.00,
  "dataVigenciaInicio": "2026-01-01",
  "garantiasExigidas": []
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `bancoId` | guid | Sim | Banco titular do limite |
| `modalidade` | string | Sim | Modalidade ao qual o limite se aplica |
| `valorLimiteBrl` | decimal | Sim | > 0 |
| `dataVigenciaInicio` | date | Sim | — |
| `dataVigenciaFim` | date | Não | Limite vigente indefinidamente se omitido |
| `observacoes` | string | Não | — |
| `garantiasExigidas` | [CriarGarantiaExigidaLimiteRequest](#criargarantiaexigidalimiterequest)[] | Não | Omitir ou enviar `null` preserva sem garantias; enviar `[]` equivale a linha no aval |

**Responses:**
- `201 Created` — [LimiteBancoDto](#limitebancodto)
- `400 Bad Request` — campo inválido ou regra de valor violada (ex.: percentual fora de (0, 100])
- `409 Conflict` — sobreposição de vigência para o par banco/modalidade, ou tipo de garantia duplicado na lista

---

### Atualizar Limite

```
PATCH /api/v1/limites-banco/{id}
Autorização: Admin
```

Atualiza o valor do limite e/ou as garantias exigidas com semântica PATCH:

- `novoValorLimiteBrl` nulo → preserva valor atual
- `garantiasExigidas` nulo → preserva garantias atuais
- `garantiasExigidas` com lista vazia (`[]`) → remove todas as garantias
- `garantiasExigidas` com itens → substitui toda a coleção (replace-all)

Quando `novoValorLimiteBrl` é informado e difere do valor atual, uma nova entrada é registrada automaticamente no histórico.

**Request Body:**

```json
{
  "novoValorLimiteBrl": 75000000.00,
  "garantiasExigidas": [
    {
      "tipo": "CdbCativo",
      "percentualSobreLimite": 20.0,
      "obrigatoria": true
    },
    {
      "tipo": "Aval",
      "obrigatoria": false,
      "observacoes": "Aval dos sócios negociável"
    }
  ]
}
```

**Exemplo — PATCH substituindo garantias, preservando valor:**

```json
{
  "garantiasExigidas": [
    {
      "tipo": "AlienacaoFiduciaria",
      "valorFixoBrl": 5000000.00,
      "obrigatoria": true,
      "observacoes": "Imóvel comercial registrado"
    }
  ]
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `novoValorLimiteBrl` | decimal | Não | > 0; não pode ser menor que `valorUtilizadoBrl` corrente |
| `garantiasExigidas` | [CriarGarantiaExigidaLimiteRequest](#criargarantiaexigidalimiterequest)[] | Não | `null` = preservar; `[]` = remover todas; itens = replace-all |

**Responses:**
- `200 OK` — [LimiteBancoDto](#limitebancodto)
- `400 Bad Request` — campo inválido ou valor abaixo do utilizado
- `404 Not Found`
- `409 Conflict` — tipo de garantia duplicado na lista enviada

---

## Garantias Exigidas

Cada `LimiteBanco` pode ter zero ou mais garantias exigidas. A coleção representa os requisitos que o banco estabelece para liberar a linha de crédito.

### Modelo

Cada garantia pertence a um único `TipoGarantia`. Para tipos diferentes de `Aval`, exatamente um entre `percentualSobreLimite` e `valorFixoBrl` deve ser informado (são mutuamente exclusivos). Para `Aval`, ambos podem ser omitidos — representa a exigência implícita de aval dos sócios cobrindo 100% da exposição.

| Campo | Tipo | Regra |
|-------|------|-------|
| `tipo` | [TipoGarantia](#tipogarantia) | Obrigatório; único por limite |
| `percentualSobreLimite` | decimal | 0 < valor ≤ 100; exclusivo com `valorFixoBrl` |
| `valorFixoBrl` | decimal | > 0; exclusivo com `percentualSobreLimite` |
| `obrigatoria` | bool | `true` = banco exige; `false` = banco negocia |
| `observacoes` | string | Opcional |

### Invariantes

- Não podem existir duas garantias com o mesmo `TipoGarantia` no mesmo limite. Tentativas de criar duplicata retornam `409 Conflict`.
- `percentualSobreLimite` e `valorFixoBrl` são mutuamente exclusivos — ambos preenchidos retornam `400 Bad Request`.
- Para tipos diferentes de `Aval`: omitir ambos retorna `400 Bad Request`.
- Operações de replace-all (`PATCH` com lista) validam a ausência de duplicatas antes de aplicar qualquer alteração.

### TipoGarantia

| Valor (string) | Int | Descrição |
|----------------|-----|-----------|
| `CdbCativo` | 1 | CDB cativo no banco credor |
| `Sblc` | 2 | Stand-by Letter of Credit |
| `Aval` | 3 | Aval de sócio/empresa (ambas quantificações opcionais) |
| `AlienacaoFiduciaria` | 4 | Alienação fiduciária de bem |
| `Duplicatas` | 5 | Caução de duplicatas |
| `RecebiveisCartao` | 6 | Cessão de recebíveis de cartão |
| `BoletoBancario` | 7 | Caução de boletos bancários |
| `Fgi` | 8 | Cobertura pelo Fundo de Garantia para Investimentos |

> O enum é serializado pelo **nome textual** (ex.: `"CdbCativo"`). A API aceita valores case-insensitive na entrada.

---

## Histórico de Valor Concedido

O campo `valorLimiteBrl` de um `LimiteBanco` é versionado automaticamente. Toda vez que o valor muda via `PATCH`, o sistema registra uma entrada no histórico com o valor anterior e o novo. A criação do limite registra a entrada inicial com `valorAnteriorBrl = null`.

O histórico serve à análise de tendência: identifica bancos que reduzem ou aumentam o limite ao longo do tempo, permitindo decisões de diversificação de captação.

O histórico é retornado pela propriedade `historico` no [LimiteBancoDto](#limitebancodto), ordenado por `registradoEm` crescente. O `GET /api/v1/limites-banco` (listagem) **não** inclui `historico` e `garantiasExigidas` por padrão — use `GET /api/v1/limites-banco/{id}` para o DTO completo.

---

## Schemas

### LimiteBancoDto

```json
{
  "id": "guid",
  "bancoId": "guid",
  "modalidade": "Finimp | Lei4131 | Refinimp | Nce | BalcaoCaixa | Fgi",
  "valorLimiteBrl": 50000000.00,
  "valorUtilizadoBrl": 12000000.00,
  "valorDisponivelBrl": 38000000.00,
  "dataVigenciaInicio": "YYYY-MM-DD",
  "dataVigenciaFim": "YYYY-MM-DD | null",
  "observacoes": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset",
  "garantiasExigidas": [GarantiaExigidaLimiteDto],
  "historico": [LimiteBancoHistoricoDto]
}
```

> `valorDisponivelBrl = valorLimiteBrl − valorUtilizadoBrl`. O `valorUtilizadoBrl` é mantido pela API: incrementado em `POST /api/v1/cotacoes/{id}/converter-em-contrato` e decrementado quando um contrato derivado é liquidado/cancelado.
>
> `historico` é ordenado por `registradoEm` crescente.

---

### GarantiaExigidaLimiteDto

```json
{
  "id": "guid",
  "tipo": "CdbCativo | Sblc | Aval | AlienacaoFiduciaria | Duplicatas | RecebiveisCartao | BoletoBancario | Fgi",
  "percentualSobreLimite": 20.0,
  "valorFixoBrl": null,
  "obrigatoria": true,
  "observacoes": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

---

### LimiteBancoHistoricoDto

```json
{
  "id": "guid",
  "limiteBancoId": "guid",
  "valorAnteriorBrl": null,
  "valorNovoBrl": 50000000.00,
  "registradoEm": "DateTimeOffset",
  "observacoes": "Criação do limite | null"
}
```

> `valorAnteriorBrl` é `null` na entrada de criação do limite. Entradas subsequentes sempre têm o valor anterior preenchido.

---

### CriarGarantiaExigidaLimiteRequest

```json
{
  "tipo": "CdbCativo",
  "percentualSobreLimite": 20.0,
  "valorFixoBrl": null,
  "obrigatoria": true,
  "observacoes": "string | null"
}
```

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `tipo` | string | — | Nome do enum `TipoGarantia` (case-insensitive) |
| `percentualSobreLimite` | decimal | `null` | Percentual sobre o limite; (0, 100]; exclusivo com `valorFixoBrl` |
| `valorFixoBrl` | decimal | `null` | Valor fixo em BRL; > 0; exclusivo com `percentualSobreLimite` |
| `obrigatoria` | bool | `true` | `true` = banco exige; `false` = banco negocia |
| `observacoes` | string | `null` | — |

---

## Regras de Validação

1. Não permite criar limite cujo período de vigência se sobreponha a outro já existente para o mesmo par `bancoId` × `modalidade`.
2. `valorLimiteBrl` não pode ser reduzido abaixo do `valorUtilizadoBrl` corrente.
3. Cotação só aceita banco-alvo se `valorAlvoBrl ≤ valorDisponivelBrl` na modalidade da cotação.
4. Não pode haver duas garantias com o mesmo `TipoGarantia` no mesmo limite.
5. `percentualSobreLimite` e `valorFixoBrl` são mutuamente exclusivos por garantia.
6. Para tipos diferentes de `Aval`, ao menos um entre `percentualSobreLimite` e `valorFixoBrl` deve ser informado.

---

## Referências

- [SPEC do módulo Cotações §3.2](../specs/cotacoes/SPEC.md)
- [Cotações API](./cotacoes.md) — consumidor do limite
- [Schemas compartilhados](./schemas.md#tipogarantia)
- [Coleção Bruno — 11-LimitesBanco](./collections/sgcf-api/11-LimitesBanco/)

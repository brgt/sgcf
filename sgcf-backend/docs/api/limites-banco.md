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
- `409 Conflict` — sobreposição de vigência para o par banco/modalidade, tipo de garantia duplicado na lista, ou banco em regime `GlobalPuro` (REG-01 — bancos de limite global puro não admitem limite por modalidade; ver [Bancos API → Regime de Limite](./bancos.md#regime-de-limite))

> **REG-01 / regime de limite:** se o banco opera em regime `GlobalPuro`, a criação e a atualização de `LimiteBanco` por modalidade são bloqueadas (`409`). Esses bancos operam exclusivamente sob o `LimiteGlobalBanco`. O mesmo se aplica a `PUT /api/v1/limites-banco/{id}`.

---

### Atualizar Limite

```
PATCH /api/v1/limites-banco/{id}
Autorização: Admin
```

Atualiza o limite com semântica PATCH — campos omitidos (null) preservam o valor atual:

- `novoValorLimiteBrl` nulo → preserva valor atual
- `garantiasExigidas` nulo → preserva garantias atuais; `[]` → remove todas; itens → replace-all
- `novaDataVigenciaFim` nulo → preserva vigência atual; informado → registra data de encerramento (RV-01)
- `novaDataVigenciaInicio` nulo → preserva início atual; informado → ajusta início da vigência
- `motivoEncerramento` nulo → preserva; informado → registra motivo (usado em conjunto com encerramento)

Quando `novoValorLimiteBrl` é informado e difere do valor atual, uma nova entrada é registrada no histórico.

> **Contrato de resposta:** este endpoint sempre retorna **[AtualizarLimiteBancoResponse](#atualizarlimitebancoresponse)** `{ limite, avisos }`. O campo `avisos` contém alertas não bloqueantes (ex.: limite com utilização ativa sendo encerrado). **Clientes devem ler os dados em `response.limite.*`, não na raiz do objeto.**

**Request Body:**

```json
{
  "novoValorLimiteBrl": 75000000.00,
  "novaDataVigenciaFim": "2026-12-31",
  "motivoEncerramento": "Banco não renovou linha para 2027",
  "garantiasExigidas": [
    {
      "tipo": "CdbCativo",
      "percentualSobreLimite": 20.0,
      "obrigatoria": true
    }
  ]
}
```

**Exemplo — encerrar vigência preservando demais campos:**

```json
{
  "novaDataVigenciaFim": "2026-06-30",
  "motivoEncerramento": "Reavaliação de crédito — comitê mai/2026"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `novoValorLimiteBrl` | decimal | Não | > 0 |
| `novaDataVigenciaFim` | date | Não | Encerra vigência na data informada |
| `novaDataVigenciaInicio` | date | Não | Ajusta início da vigência |
| `motivoEncerramento` | string | Não | Registrado no limite encerrado. Requer `novaDataVigenciaFim` no mesmo request — `400` se informado isoladamente |
| `garantiasExigidas` | [CriarGarantiaExigidaLimiteRequest](#criargarantiaexigidalimiterequest)[] | Não | `null` = preservar; `[]` = remover todas; itens = replace-all |
| `configurarAntecipacao` | bool | Não | `true` para atualizar os campos de antecipação abaixo |
| `padraoAntecipacao` | string | Não | Ver [PadraoAntecipacao](./schemas.md#padraoantecipacao) |
| `breakFundingFeePct` | decimal | Não | ≥ 0 |
| `tlaPctSobreSaldo` | decimal | Não | ≥ 0 |
| `tlaPctPorMesRemanescente` | decimal | Não | ≥ 0 |
| `valorMinimoParcialPct` | decimal | Não | ≥ 0 |
| `observacoesAntecipacao` | string | Não | — |

**Responses:**
- `200 OK` — [AtualizarLimiteBancoResponse](#atualizarlimitebancoresponse) `{ limite, avisos }`
- `400 Bad Request` — campo inválido ou `motivoEncerramento` informado sem `novaDataVigenciaFim`
- `404 Not Found`
- `409 Conflict` — a nova vigência causa sobreposição com outro limite existente (RV-01-B)

---

### Substituir Limite (Reavaliação de Crédito)

```
POST /api/v1/limites-banco/{id}/substituir
Autorização: Admin
```

> **Adicionado em [0.12.0].**

Operação atômica de reavaliação de crédito: encerra o limite atual e cria um sucessor em uma única transação. Fluxo esperado:

1. O banco concede novo limite (valor, vigência) após reavaliação do crédito.
2. O sistema registra `DataVigenciaFim = novoInicio − 1 dia` no limite atual.
3. O limite sucessor é criado com os novos parâmetros, sem herdar configurações de antecipação.

Use este endpoint em vez de `PATCH` quando o objetivo for registrar uma nova concessão formal de crédito, preservando o histórico do limite anterior para auditoria.

**Request Body:**

```json
{
  "novoInicio": "2027-01-01",
  "novoValorLimiteBrl": 80000000.00,
  "novaDataVigenciaFim": "2027-12-31",
  "motivoEncerramento": "Renovação anual — comitê mai/2026",
  "observacoes": "Limite renovado com aumento de 60%",
  "garantiasExigidas": [
    {
      "tipo": "CdbCativo",
      "percentualSobreLimite": 15.0,
      "obrigatoria": true
    }
  ]
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `novoInicio` | date | Sim | Início da vigência do sucessor. Deve ser posterior ao início do limite atual (RV-02-A) |
| `novoValorLimiteBrl` | decimal | Sim | > 0 |
| `novaDataVigenciaFim` | date | Não | Data de fim da vigência do sucessor. Omitir cria limite de vigência indefinida |
| `motivoEncerramento` | string | Não | Registrado no limite encerrado |
| `observacoes` | string | Não | Observações do limite sucessor |
| `garantiasExigidas` | [CriarGarantiaExigidaLimiteRequest](#criargarantiaexigidalimiterequest)[] | Não | Garantias do sucessor. Omitir cria sucessor sem garantias |

**Responses:**
- `201 Created` — [LimiteBancoDto](#limitebancodto) do sucessor (com `Location` apontando para `GET /limites-banco/{sucessorId}`)
- `400 Bad Request` — `novoInicio` não é posterior ao início do limite atual (RV-02-A)
- `404 Not Found` — limite não encontrado
- `409 Conflict` — a vigência do sucessor causa sobreposição com outro limite existente (RV-02-D), ou novo valor supera o limite global do banco (LG-09)

---

### Listar Revisões de Garantias

```
GET /api/v1/limites-banco/{id}/revisoes-garantias
Autorização: Leitura
```

Retorna o histórico temporal completo das políticas de garantia do limite, ordenado por `vigenciaInicio` crescente. Cada revisão representa um intervalo de vigência com seu conjunto de itens.

> **Adicionado em [0.11.0] (S34).**

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do limite |

**Responses:**
- `200 OK` — [ListarRevisoesGarantiasResponse](#listarevisoesgarantiasresponse)
- `404 Not Found`

---

### Remover Garantia Exigida por Tipo

```
DELETE /api/v1/limites-banco/{id}/garantias-exigidas?tipo=X
Autorização: Admin
```

Remove uma garantia exigida pelo tipo. Se a revisão vigente continha o tipo informado, uma nova revisão é criada sem esse item. A operação é **idempotente**: se o tipo não existia na revisão vigente, retorna `204` sem criar nova revisão.

> **Adicionado em [0.11.0] (S34).**

**Path Parameters:**

| Parâmetro | Tipo | Descrição |
|-----------|------|-----------|
| `id` | guid | ID do limite |

**Query Parameters:**

| Parâmetro | Tipo | Obrigatório | Descrição |
|-----------|------|-------------|-----------|
| `tipo` | string | Sim | Nome do [TipoGarantia](#tipogarantia) a remover (case-insensitive) |

**Responses:**
- `204 No Content` — Garantia removida (ou tipo já ausente — idempotente)
- `400 Bad Request` — `tipo` ausente ou inválido
- `404 Not Found`

---

## Garantias Exigidas

Cada `LimiteBanco` pode ter zero ou mais garantias exigidas. A coleção representa os requisitos que o banco estabelece para liberar a linha de crédito.

### Versionamento Temporal

A partir de `[0.11.0]` (S34), as garantias exigidas são gerenciadas por **revisões temporais** (`GarantiaExigidaRevisao`). Cada limite possui no máximo uma revisão **vigente** (sem `vigenciaFim`) em qualquer instante.

Comportamento do campo `garantiasExigidas` no `PATCH /api/v1/limites-banco/{id}`:

| Valor enviado | Efeito |
|---------------|--------|
| `null` (omitido) | Preserva a revisão vigente — nenhuma nova revisão é criada |
| `[]` (lista vazia) | Cria nova revisão vazia (sem garantias exigidas) |
| Lista com itens idênticos à revisão vigente | **Idempotente** — sem nova revisão |
| Lista com itens diferentes | Fecha a revisão vigente e cria nova com os novos itens |

O campo `garantiasExigidas` no `LimiteBancoDto` continua expondo os itens da revisão vigente, compatível com clientes anteriores à S34. Use `GET /revisoes-garantias` para o histórico completo.

### Modelo

Cada garantia pertence a um único `TipoGarantia`. Para tipos diferentes de `Aval`, exatamente um entre `percentualSobreLimite` e `valorFixoBrl` deve ser informado (são mutuamente exclusivos). Para `Aval`, ambos podem ser omitidos — representa a exigência implícita de aval dos sócios cobrindo 100% da exposição.

| Campo | Tipo | Regra |
|-------|------|-------|
| `tipo` | [TipoGarantia](#tipogarantia) | Obrigatório; único por limite |
| `percentualSobreLimite` | decimal | 0 < valor ≤ 100; exclusivo com `valorFixoBrl` |
| `valorFixoBrl` | decimal | > 0; exclusivo com `percentualSobreLimite` |
| `obrigatoria` | bool | `true` = banco exige; `false` = banco negocia |
| `observacoes` | string | Opcional |
| `grupoAlternativaId` | guid | Opcional. Identifica o grupo de alternativas "OU" ao qual o item pertence. `null` = item independente |
| `grupoRotulo` | string | Opcional; ≤ 120 caracteres. Rótulo legível do grupo. Só faz sentido com `grupoAlternativaId`; é ignorado (normalizado para `null`) em itens independentes |

### Invariantes

- Não podem existir duas garantias com o mesmo `TipoGarantia` no mesmo limite. Tentativas de criar duplicata retornam `409 Conflict`.
- `percentualSobreLimite` e `valorFixoBrl` são mutuamente exclusivos — ambos preenchidos retornam `400 Bad Request`.
- Para tipos diferentes de `Aval`: omitir ambos retorna `400 Bad Request`.
- Operações de replace-all (`PATCH` com lista) validam a ausência de duplicatas antes de aplicar qualquer alteração.

### Grupos de Alternativas "OU"

> **Adicionado em [0.13.0] (S36).**

Um conjunto de itens que compartilham o mesmo `grupoAlternativaId` forma um **grupo de alternativas mutuamente substituíveis** ("OU"). O banco aceita qualquer uma das alternativas — ou a combinação parcial delas — para satisfazer a exigência do grupo. Exemplo: "CDB cativo de 100% do principal **OU** caução de boletos bancários de 100% do principal".

Regras do grupo:

- **Sempre obrigatório.** Todo item agrupado é tratado como `obrigatoria = true` no enforcement, independentemente do `obrigatoria` enviado por item. O grupo, como um todo, precisa ser coberto na conversão cotação→contrato.
- **Valor exigido do grupo = mínimo das alternativas.** A contribuição do grupo para o valor total de garantia exigida é o **menor** valor individual entre suas alternativas (o piso mais barato capaz de satisfazer o grupo), não a soma. Itens independentes (sem `grupoAlternativaId`) continuam somando normalmente.
- **Cobertura por fração normalizada.** Na conversão, o grupo é considerado coberto quando a soma das frações de cobertura de cada alternativa atinge ou supera 1,0:

  ```
  Σ min(valorCoberto_A / valorAlvo_A, 1.0) ≥ 1.0
  ```

  Isso permite tanto cobrir o grupo com uma única alternativa a 100% quanto combinar parcelas de alternativas distintas (ex.: 60% em CDB cativo + 40% em boletos). A fração é arredondada a 6 casas (`AwayFromZero`) antes da comparação.
- **Rótulo único por grupo.** Todos os itens do mesmo grupo devem usar o mesmo `grupoRotulo` (ou omiti-lo). O rótulo, quando presente, identifica o grupo nas respostas de lacuna.

#### Relato de lacuna de grupo (409 na conversão)

Quando uma cotação é convertida em contrato (`POST /api/v1/cotacoes/{id}/converter-em-contrato`) e um grupo "OU" não atinge a fração mínima de cobertura, a conversão é bloqueada com `409 Conflict`. O corpo segue o formato `ProblemDetails` (RFC 7807) com a extensão `lacunas`. Uma lacuna de **grupo** preenche os campos `grupoAlternativaId`, `grupoRotulo`, `alternativasAceitas` e `fracaoCoberta` — enquanto `valorEsperadoBrl` e `valorCobertoBrl` ficam `null` (a regra de grupo é baseada em fração, não em valor único):

```json
{
  "type": "https://sgcf.io/errors/garantia-exigida-nao-coberta",
  "title": "Garantias exigidas pela política do banco não foram cobertas pelo contrato.",
  "status": 409,
  "detail": "A revisão vigente do LimiteBanco {id} exige 1 garantia(s) obrigatória(s) que não foram supridas.",
  "limiteBancoId": "guid",
  "garantiasExigidasRevisaoId": "guid",
  "lacunas": [
    {
      "tipo": "CDB OU Recebíveis",
      "obrigatoria": true,
      "valorEsperadoBrl": null,
      "valorCobertoBrl": null,
      "grupoAlternativaId": "8f1c3b2a-0d4e-4a6b-9c8d-1e2f3a4b5c6d",
      "grupoRotulo": "CDB OU Recebíveis",
      "alternativasAceitas": ["CdbCativo", "BoletoBancario"],
      "fracaoCoberta": 0.6
    }
  ]
}
```

| Campo da lacuna | Tipo | Lacuna de item | Lacuna de grupo |
|-----------------|------|----------------|-----------------|
| `tipo` | string | Nome do `TipoGarantia` | `grupoRotulo` se presente, senão `"Grupo: Tipo1 OU Tipo2"` |
| `valorEsperadoBrl` | decimal \| null | Valor mínimo exigido | `null` |
| `valorCobertoBrl` | decimal \| null | Valor coberto pelo contrato | `null` |
| `grupoAlternativaId` | guid \| null | `null` | Id do grupo "OU" |
| `grupoRotulo` | string \| null | `null` | Rótulo do grupo, quando informado |
| `alternativasAceitas` | string[] \| null | `null` | Tipos aceitos no grupo |
| `fracaoCoberta` | decimal \| null | `null` | Fração coberta acumulada (0,0 ≤ valor < 1,0), 6 casas `AwayFromZero` |

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
  "motivoEncerramento": "string | null",
  "padraoAntecipacao": "A | B | null",
  "breakFundingFeePct": 0.5,
  "tlaPctSobreSaldo": null,
  "tlaPctPorMesRemanescente": null,
  "valorMinimoParcialPct": null,
  "observacoesAntecipacao": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset",
  "garantiasExigidas": [GarantiaExigidaItemDto],
  "historico": [LimiteBancoHistoricoDto]
}
```

> `valorDisponivelBrl = valorLimiteBrl − valorUtilizadoBrl`. O `valorUtilizadoBrl` é mantido pela API: incrementado em `POST /api/v1/cotacoes/{id}/converter-em-contrato` e decrementado quando um contrato derivado é liquidado/cancelado.
>
> `historico` é ordenado por `registradoEm` crescente.
>
> `motivoEncerramento` é preenchido quando o limite é encerrado via `PATCH` com `motivoEncerramento` ou via `POST /substituir`.

---

### AtualizarLimiteBancoResponse

Retornado pelo `PATCH /limites-banco/{id}` em todos os casos.

```json
{
  "limite": { ...LimiteBancoDto },
  "avisos": [
    "Este limite possui BRL 3.000.000 em utilização ativa. Contratos vinculados não são afetados, mas nenhuma nova cotação poderá usar este limite após 2026-12-31."
  ]
}
```

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `limite` | [LimiteBancoDto](#limitebancodto) | Limite atualizado |
| `avisos` | string[] | Alertas não bloqueantes. Vazio quando não há utilização ativa |

---

### GarantiaExigidaItemDto

> Renomeado de `GarantiaExigidaLimiteDto` em [0.11.0] (S34). O contrato JSON não mudou.

```json
{
  "id": "guid",
  "tipo": "CdbCativo | Sblc | Aval | AlienacaoFiduciaria | Duplicatas | RecebiveisCartao | BoletoBancario | Fgi",
  "percentualSobreLimite": 20.0,
  "valorFixoBrl": null,
  "obrigatoria": true,
  "observacoes": "string | null",
  "grupoAlternativaId": "guid | null",
  "grupoRotulo": "string | null",
  "createdAt": "DateTimeOffset",
  "updatedAt": "DateTimeOffset"
}
```

> `grupoAlternativaId` e `grupoRotulo` foram adicionados em [0.13.0] (S36). São `null` em itens independentes (comportamento legado preservado).

---

### ListarRevisoesGarantiasResponse

```json
{
  "limiteBancoId": "guid",
  "revisoes": [GarantiaExigidaRevisaoDto]
}
```

---

### GarantiaExigidaRevisaoDto

```json
{
  "id": "guid",
  "limiteBancoId": "guid",
  "vigenciaInicio": "DateTimeOffset",
  "vigenciaFim": "DateTimeOffset | null",
  "motivo": "string | null",
  "itens": [GarantiaExigidaItemDto]
}
```

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `vigenciaInicio` | DateTimeOffset | Instante em que a revisão entrou em vigor |
| `vigenciaFim` | DateTimeOffset \| null | `null` indica a revisão **vigente**; preenchido nas revisões encerradas |
| `motivo` | string \| null | Descrição do motivo da revisão (ex.: "Ajuste em comitê de crédito") |
| `itens` | GarantiaExigidaItemDto[] | Itens de garantia desta revisão; vazio se a revisão não exige garantias |

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
  "observacoes": "string | null",
  "grupoAlternativaId": null,
  "grupoRotulo": null
}
```

| Campo | Tipo | Default | Descrição |
|-------|------|---------|-----------|
| `tipo` | string | — | Nome do enum `TipoGarantia` (case-insensitive) |
| `percentualSobreLimite` | decimal | `null` | Percentual sobre o limite; (0, 100]; exclusivo com `valorFixoBrl` |
| `valorFixoBrl` | decimal | `null` | Valor fixo em BRL; > 0; exclusivo com `percentualSobreLimite` |
| `obrigatoria` | bool | `true` | `true` = banco exige; `false` = banco negocia. Ignorado para itens agrupados (sempre obrigatório) |
| `observacoes` | string | `null` | — |
| `grupoAlternativaId` | guid | `null` | Grupo de alternativas "OU". Itens com o mesmo valor formam um grupo mutuamente substituível. Não pode ser `Guid.Empty` |
| `grupoRotulo` | string | `null` | Rótulo do grupo; ≤ 120 caracteres. Deve ser consistente entre os itens do mesmo grupo; ignorado em itens independentes |

**Exemplo — grupo "CDB OU Recebíveis" (PATCH ou POST):**

```json
{
  "garantiasExigidas": [
    {
      "tipo": "CdbCativo",
      "percentualSobreLimite": 100.0,
      "obrigatoria": true,
      "grupoAlternativaId": "8f1c3b2a-0d4e-4a6b-9c8d-1e2f3a4b5c6d",
      "grupoRotulo": "CDB OU Recebíveis"
    },
    {
      "tipo": "BoletoBancario",
      "percentualSobreLimite": 100.0,
      "obrigatoria": true,
      "grupoAlternativaId": "8f1c3b2a-0d4e-4a6b-9c8d-1e2f3a4b5c6d",
      "grupoRotulo": "CDB OU Recebíveis"
    }
  ]
}
```

> Neste exemplo, ambas as alternativas exigem 100% do principal. O valor exigido do grupo é o **mínimo** entre as contribuições (100% × principal em cada uma — iguais aqui), e não a soma de 200%. Na conversão, o grupo é satisfeito por CDB cativo a 100%, por boletos a 100%, ou por qualquer combinação cujas frações somem ≥ 1,0.

---

## Regras de Validação

1. Não permite criar limite cujo período de vigência se sobreponha a outro já existente para o mesmo par `bancoId` × `modalidade`.
2. `valorLimiteBrl` não pode ser reduzido abaixo do `valorUtilizadoBrl` corrente.
3. Cotação só aceita banco-alvo se `valorAlvoBrl ≤ valorDisponivelBrl` na modalidade da cotação.
4. Não pode haver duas garantias com o mesmo `TipoGarantia` no mesmo limite.
5. `percentualSobreLimite` e `valorFixoBrl` são mutuamente exclusivos por garantia.
6. Para tipos diferentes de `Aval`, ao menos um entre `percentualSobreLimite` e `valorFixoBrl` deve ser informado.
7. **RV-01-B**: ao ajustar `novaDataVigenciaFim` ou `novaDataVigenciaInicio` via PATCH, a nova vigência não pode sobrepor outro limite do mesmo par banco/modalidade (excluindo o próprio limite).
8. **RV-01-C** (PATCH): `motivoEncerramento` só pode ser enviado quando `novaDataVigenciaFim` também está presente no mesmo request — caso contrário retorna `400`.
9. **RV-02-A**: em `POST /substituir`, `novoInicio` deve ser estritamente posterior à `dataVigenciaInicio` do limite atual.
10. **RV-02-C**: em `POST /substituir`, quando `novaDataVigenciaFim` é informado, deve ser estritamente posterior a `novoInicio` — caso contrário retorna `400`.
11. **RV-02-D**: em `POST /substituir`, a vigência do sucessor não pode sobrepor outro limite existente do par banco/modalidade (excluindo o limite sendo substituído).
12. **LG-09**: o valor do limite por modalidade não pode superar o limite global vigente do banco (quando houver limite global cadastrado).

---

## Referências

- [SPEC do módulo Cotações §3.2](../specs/cotacoes/SPEC.md)
- [Cotações API](./cotacoes.md) — consumidor do limite
- [Schemas compartilhados](./schemas.md#tipogarantia)
- [Coleção Bruno — 11-LimitesBanco](./collections/sgcf-api/11-LimitesBanco/)
